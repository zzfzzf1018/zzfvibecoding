"""桌面端估值计算器：DCF 折现、复利、动态 PE。"""

import tkinter as tk
from tkinter import messagebox, ttk

import core

PERIOD_CHOICES = {
    "每年 (1)": 1,
    "每半年 (2)": 2,
    "每季度 (4)": 4,
    "每月 (12)": 12,
}


def parse_number(text: str, label: str, *, allow_empty: bool = False) -> float | None:
    text = text.strip()
    if not text:
        if allow_empty:
            return None
        raise ValueError(f"请填写「{label}」")
    try:
        return float(text)
    except ValueError:
        raise ValueError(f"「{label}」必须是数字，当前值：{text}") from None


def parse_int(text: str, label: str) -> int:
    value = parse_number(text, label)
    assert value is not None
    if value != int(value):
        raise ValueError(f"「{label}」必须是整数")
    return int(value)


def money(value: float) -> str:
    return f"{value:,.2f}"


def percent(value: float) -> str:
    return f"{value * 100:.2f}%"


class CalculatorTab(ttk.Frame):
    """带表单、摘要和明细表格的计算页基类。"""

    title = ""
    columns: tuple[tuple[str, str, int], ...] = ()

    def __init__(self, master: tk.Misc) -> None:
        super().__init__(master, padding=12)
        self.entries: dict[str, ttk.Entry | ttk.Combobox] = {}

        self.form = ttk.LabelFrame(self, text="参数", padding=10)
        self.form.pack(fill="x")
        self.form.columnconfigure(1, weight=1)
        self.form.columnconfigure(3, weight=1)
        self._row = 0
        self._col = 0

        self.build_form()

        buttons = ttk.Frame(self, padding=(0, 10))
        buttons.pack(fill="x")
        ttk.Button(buttons, text="计算", command=self.on_calculate).pack(side="left")
        ttk.Button(buttons, text="重置", command=self.on_reset).pack(side="left", padx=6)

        self.summary = tk.Text(self, height=5, wrap="word", state="disabled",
                               background="#f5f5f5", relief="flat", padx=8, pady=6)
        self.summary.pack(fill="x")

        table_frame = ttk.Frame(self, padding=(0, 10, 0, 0))
        table_frame.pack(fill="both", expand=True)
        keys = [key for key, _, _ in self.columns]
        self.tree = ttk.Treeview(table_frame, columns=keys, show="headings", height=10)
        for key, heading, width in self.columns:
            self.tree.heading(key, text=heading)
            self.tree.column(key, width=width, anchor="e")
        scroll = ttk.Scrollbar(table_frame, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scroll.set)
        self.tree.pack(side="left", fill="both", expand=True)
        scroll.pack(side="right", fill="y")

        self.defaults = {key: widget.get() for key, widget in self.entries.items()}

    def add_field(self, key: str, label: str, default: str = "", hint: str = "") -> None:
        ttk.Label(self.form, text=label).grid(row=self._row, column=self._col * 2,
                                              sticky="w", padx=(0, 6), pady=4)
        entry = ttk.Entry(self.form, width=16)
        entry.insert(0, default)
        entry.grid(row=self._row, column=self._col * 2 + 1, sticky="ew", padx=(0, 20), pady=4)
        if hint:
            self._add_hint(entry, hint)
        self.entries[key] = entry
        self._advance()

    def add_choice(self, key: str, label: str, choices: list[str], default: str) -> None:
        ttk.Label(self.form, text=label).grid(row=self._row, column=self._col * 2,
                                              sticky="w", padx=(0, 6), pady=4)
        box = ttk.Combobox(self.form, values=choices, state="readonly", width=14)
        box.set(default)
        box.grid(row=self._row, column=self._col * 2 + 1, sticky="ew", padx=(0, 20), pady=4)
        self.entries[key] = box
        self._advance()

    def _add_hint(self, entry: ttk.Entry, hint: str) -> None:
        ttk.Label(self.form, text=hint, foreground="#777").grid(
            row=self._row + 1, column=self._col * 2 + 1, sticky="w", padx=(0, 20))

    def _advance(self) -> None:
        if self._col == 0:
            self._col = 1
        else:
            self._col = 0
            self._row += 2

    def value(self, key: str) -> str:
        return self.entries[key].get()

    def build_form(self) -> None:
        raise NotImplementedError

    def calculate(self) -> tuple[str, list[tuple]]:
        """返回 (摘要文本, 表格行数据)。"""
        raise NotImplementedError

    def on_calculate(self) -> None:
        try:
            summary, rows = self.calculate()
        except ValueError as exc:
            messagebox.showerror("输入有误", str(exc), parent=self)
            return
        self.set_summary(summary)
        self.tree.delete(*self.tree.get_children())
        for row in rows:
            self.tree.insert("", "end", values=row)

    def on_reset(self) -> None:
        for key, widget in self.entries.items():
            if isinstance(widget, ttk.Combobox):
                widget.set(self.defaults[key])
            else:
                widget.delete(0, "end")
                widget.insert(0, self.defaults[key])
        self.set_summary("")
        self.tree.delete(*self.tree.get_children())

    def set_summary(self, text: str) -> None:
        self.summary.configure(state="normal")
        self.summary.delete("1.0", "end")
        self.summary.insert("1.0", text)
        self.summary.configure(state="disabled")


class DcfTab(CalculatorTab):
    title = "DCF 折现"
    columns = (
        ("year", "年份", 60),
        ("cash_flow", "预测现金流", 140),
        ("factor", "折现系数", 110),
        ("pv", "现值", 140),
    )

    def build_form(self) -> None:
        self.add_field("cash_flow", "当前现金流", "100")
        self.add_field("discount_rate", "折现率 (%)", "10")
        self.add_field("growth_rate", "增长率 (%)", "8")
        self.add_field("years", "预测年限", "10")
        self.add_field("terminal_growth", "永续增长率 (%)", "", hint="留空则不计终值")

    def calculate(self) -> tuple[str, list[tuple]]:
        cash_flow = parse_number(self.value("cash_flow"), "当前现金流")
        discount_rate = parse_number(self.value("discount_rate"), "折现率")
        growth_rate = parse_number(self.value("growth_rate"), "增长率")
        years = parse_int(self.value("years"), "预测年限")
        terminal = parse_number(self.value("terminal_growth"), "永续增长率", allow_empty=True)

        assert cash_flow is not None and discount_rate is not None and growth_rate is not None
        result = core.dcf(
            current_cash_flow=cash_flow,
            discount_rate=discount_rate / 100,
            growth_rate=growth_rate / 100,
            years=years,
            terminal_growth=None if terminal is None else terminal / 100,
        )

        lines = [
            f"预测期现值合计：{money(result.explicit_pv)}",
        ]
        if result.terminal_value is not None and result.terminal_pv is not None:
            lines.append(f"终值：{money(result.terminal_value)}    终值现值：{money(result.terminal_pv)}")
        lines.append(f"内在价值合计（折现到现在）：{money(result.total_pv)}")

        rows = [
            (row.year, money(row.cash_flow), f"{row.discount_factor:.4f}", money(row.present_value))
            for row in result.rows
        ]
        return "\n".join(lines), rows


class CompoundTab(CalculatorTab):
    title = "复利计算"
    columns = (
        ("year", "年份", 60),
        ("begin", "期初金额", 140),
        ("contribution", "本年追加", 130),
        ("interest", "本年收益", 130),
        ("end", "期末金额", 150),
    )

    def build_form(self) -> None:
        self.add_field("principal", "初始本金", "10000")
        self.add_field("rate", "年化收益率 (%)", "8")
        self.add_field("years", "投资年限", "20")
        self.add_choice("periods", "计息频率", list(PERIOD_CHOICES), "每年 (1)")
        self.add_field("contribution", "每期追加投入", "0")

    def calculate(self) -> tuple[str, list[tuple]]:
        principal = parse_number(self.value("principal"), "初始本金")
        rate = parse_number(self.value("rate"), "年化收益率")
        years = parse_int(self.value("years"), "投资年限")
        contribution = parse_number(self.value("contribution"), "每期追加投入")
        periods = PERIOD_CHOICES[self.value("periods")]

        assert principal is not None and rate is not None and contribution is not None
        result = core.compound(
            principal=principal,
            annual_rate=rate / 100,
            years=years,
            periods_per_year=periods,
            contribution_per_period=contribution,
        )

        multiple = result.final_balance / result.total_invested if result.total_invested else 0.0
        summary = (
            f"期末总额：{money(result.final_balance)}\n"
            f"累计投入：{money(result.total_invested)}    累计收益：{money(result.total_interest)}\n"
            f"资金倍数：{multiple:.2f} 倍"
        )
        rows = [
            (row.year, money(row.begin_balance), money(row.contribution),
             money(row.interest), money(row.end_balance))
            for row in result.rows
        ]
        return summary, rows


class DynamicPeTab(CalculatorTab):
    title = "动态 PE"
    columns = (
        ("year", "年份", 60),
        ("earnings", "盈利倍数", 130),
        ("pe", "动态 PE（股价不变）", 180),
    )

    def build_form(self) -> None:
        self.add_field("current_pe", "当前 PE", "30")
        self.add_field("growth_rate", "盈利年增长率 (%)", "20")
        self.add_field("years", "年限", "5")
        self.add_field("exit_pe", "N 年后 PE", "20", hint="留空则只看动态 PE")

    def calculate(self) -> tuple[str, list[tuple]]:
        current_pe = parse_number(self.value("current_pe"), "当前 PE")
        growth_rate = parse_number(self.value("growth_rate"), "盈利年增长率")
        years = parse_int(self.value("years"), "年限")
        exit_pe = parse_number(self.value("exit_pe"), "N 年后 PE", allow_empty=True)

        assert current_pe is not None and growth_rate is not None
        result = core.dynamic_pe(
            current_pe=current_pe,
            growth_rate=growth_rate / 100,
            years=years,
            exit_pe=exit_pe,
        )

        last = result.rows[-1]
        lines = [f"第 {years} 年动态 PE（按当前股价）：{last.dynamic_pe:.2f}    盈利为当前的 {last.earnings_index:.2f} 倍"]
        if result.total_return is not None and result.annualized_return is not None:
            lines.append(
                f"若 {years} 年后 PE 为 {result.exit_pe:.2f}，股价累计回报：{percent(result.total_return)}"
            )
            lines.append(f"年化回报：{percent(result.annualized_return)}")

        rows = [
            (row.year, f"{row.earnings_index:.2f}", f"{row.dynamic_pe:.2f}")
            for row in result.rows
        ]
        return "\n".join(lines), rows


def main() -> None:
    root = tk.Tk()
    root.title("估值计算器 — DCF / 复利 / 动态 PE")
    root.geometry("820x640")
    root.minsize(720, 560)

    notebook = ttk.Notebook(root)
    notebook.pack(fill="both", expand=True, padx=8, pady=8)
    for tab_class in (DcfTab, CompoundTab, DynamicPeTab):
        tab = tab_class(notebook)
        notebook.add(tab, text=tab_class.title)

    root.mainloop()


if __name__ == "__main__":
    main()
