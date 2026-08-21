import tkinter as tk
from tkinter import ttk, messagebox, simpledialog
import threading
import queue
import sys

sys.path.insert(0, '.')

from models import Stock, FinancialStatements
from data_access import AkShareDataSource, SQLiteCache, CachedDataSource
from business import StockService, StockPoolService


class StockApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("股票分析工具")
        self.geometry("1200x800")
        self.minsize(1000, 600)

        cache = SQLiteCache()
        primary_source = AkShareDataSource()
        data_source = CachedDataSource(primary_source, cache)
        self.stock_service = StockService(data_source)
        self.pool_service = StockPoolService(self.stock_service)

        self.current_stock = None
        self.current_financials = None
        self.loading_queue = queue.Queue()
        self.search_results = []

        self._setup_ui()
        self._start_loading_handler()

    def _setup_ui(self):
        main_frame = ttk.Frame(self)
        main_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        search_frame = ttk.Frame(main_frame)
        search_frame.pack(fill=tk.X, pady=(0, 10))

        ttk.Label(search_frame, text="股票代码/名称:").pack(side=tk.LEFT, padx=(0, 5))
        self.search_entry = ttk.Entry(search_frame, width=30)
        self.search_entry.pack(side=tk.LEFT, padx=(0, 5))
        self.search_entry.bind('<Return>', self._on_search)

        ttk.Button(search_frame, text="搜索", command=self._on_search).pack(side=tk.LEFT, padx=(0, 5))

        self.search_listbox = tk.Listbox(search_frame, width=40, height=1)
        self.search_listbox.pack(side=tk.LEFT, padx=(0, 5))
        self.search_listbox.bind('<<ListboxSelect>>', self._on_search_select)

        ttk.Button(search_frame, text="添加到股票池", command=self._add_to_pool).pack(side=tk.LEFT)

        self.notebook = ttk.Notebook(main_frame)
        self.notebook.pack(fill=tk.BOTH, expand=True)

        self._create_stock_info_tab()
        self._create_financial_tab()
        self._create_valuation_tab()
        self._create_pool_tab()

    def _create_stock_info_tab(self):
        tab = ttk.Frame(self.notebook)
        self.notebook.add(tab, text="股票信息")

        info_frame = ttk.LabelFrame(tab, text="基本信息")
        info_frame.pack(fill=tk.X, padx=10, pady=10)

        self.stock_info_text = tk.Text(info_frame, height=8, wrap=tk.WORD)
        self.stock_info_text.pack(fill=tk.X, padx=10, pady=10)
        self.stock_info_text.config(state=tk.DISABLED)

    def _create_financial_tab(self):
        tab = ttk.Frame(self.notebook)
        self.notebook.add(tab, text="财务报表")

        nb = ttk.Notebook(tab)
        nb.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        bs_frame = ttk.Frame(nb)
        nb.add(bs_frame, text="资产负债表")
        self.bs_tree = ttk.Treeview(bs_frame, columns=('日期', '总资产', '总负债', '净资产', '货币资金'), show='headings')
        for col in ('日期', '总资产', '总负债', '净资产', '货币资金'):
            self.bs_tree.heading(col, text=col)
            self.bs_tree.column(col, width=120, anchor='center')
        self.bs_tree.pack(fill=tk.BOTH, expand=True)

        inc_frame = ttk.Frame(nb)
        nb.add(inc_frame, text="利润表")
        self.inc_tree = ttk.Treeview(inc_frame, columns=('日期', '营业收入', '营业利润', '净利润', '归属母公司净利润', 'EPS'), show='headings')
        for col in ('日期', '营业收入', '营业利润', '净利润', '归属母公司净利润', 'EPS'):
            self.inc_tree.heading(col, text=col)
            self.inc_tree.column(col, width=110, anchor='center')
        self.inc_tree.pack(fill=tk.BOTH, expand=True)

        cf_frame = ttk.Frame(nb)
        nb.add(cf_frame, text="现金流量表")
        self.cf_tree = ttk.Treeview(cf_frame, columns=('日期', '经营活动', '投资活动', '筹资活动', '净增加额'), show='headings')
        for col in ('日期', '经营活动', '投资活动', '筹资活动', '净增加额'):
            self.cf_tree.heading(col, text=col)
            self.cf_tree.column(col, width=120, anchor='center')
        self.cf_tree.pack(fill=tk.BOTH, expand=True)

    def _create_valuation_tab(self):
        tab = ttk.Frame(self.notebook)
        self.notebook.add(tab, text="估值指标")

        pe_frame = ttk.LabelFrame(tab, text="PE指标")
        pe_frame.pack(fill=tk.X, padx=10, pady=10)

        self.pe_methods = self.stock_service.get_all_pe_methods()
        self.pe_vars = {}
        for key, name in self.pe_methods.items():
            row = ttk.Frame(pe_frame)
            row.pack(fill=tk.X, padx=10, pady=5)
            ttk.Label(row, text=name, width=15).pack(side=tk.LEFT)
            var = tk.StringVar(value="--")
            self.pe_vars[key] = var
            ttk.Label(row, textvariable=var, width=15).pack(side=tk.LEFT, padx=(10, 0))

        pb_frame = ttk.LabelFrame(tab, text="PB指标")
        pb_frame.pack(fill=tk.X, padx=10, pady=10)

        self.pb_methods = self.stock_service.get_all_pb_methods()
        self.pb_vars = {}
        for key, name in self.pb_methods.items():
            row = ttk.Frame(pb_frame)
            row.pack(fill=tk.X, padx=10, pady=5)
            ttk.Label(row, text=name, width=15).pack(side=tk.LEFT)
            var = tk.StringVar(value="--")
            self.pb_vars[key] = var
            ttk.Label(row, textvariable=var, width=15).pack(side=tk.LEFT, padx=(10, 0))

    def _create_pool_tab(self):
        tab = ttk.Frame(self.notebook)
        self.notebook.add(tab, text="股票池")

        pool_frame = ttk.Frame(tab)
        pool_frame.pack(fill=tk.X, padx=10, pady=10)

        ttk.Label(pool_frame, text="股票池:").pack(side=tk.LEFT, padx=(0, 5))
        self.pool_var = tk.StringVar()
        self.pool_combobox = ttk.Combobox(pool_frame, textvariable=self.pool_var, width=20)
        self.pool_combobox.pack(side=tk.LEFT, padx=(0, 5))
        self.pool_combobox.bind('<<ComboboxSelected>>', self._on_pool_select)

        ttk.Button(pool_frame, text="新建股票池", command=self._create_pool).pack(side=tk.LEFT, padx=(0, 5))
        ttk.Button(pool_frame, text="删除股票池", command=self._delete_pool).pack(side=tk.LEFT, padx=(0, 5))

        self.pool_tree = ttk.Treeview(tab, columns=('代码', '名称', '价格'), show='headings')
        for col in ('代码', '名称', '价格'):
            self.pool_tree.heading(col, text=col)
            self.pool_tree.column(col, width=120, anchor='center')
        self.pool_tree.pack(fill=tk.BOTH, expand=True, padx=10, pady=(0, 10))

        action_frame = ttk.Frame(tab)
        action_frame.pack(fill=tk.X, padx=10, pady=(0, 10))

        ttk.Button(action_frame, text="移除股票", command=self._remove_from_pool).pack(side=tk.LEFT, padx=(0, 5))
        ttk.Button(action_frame, text="计算平均PE", command=self._calculate_pool_pe).pack(side=tk.LEFT, padx=(0, 5))
        ttk.Button(action_frame, text="计算平均PB", command=self._calculate_pool_pb).pack(side=tk.LEFT)

        result_frame = ttk.LabelFrame(tab, text="计算结果")
        result_frame.pack(fill=tk.X, padx=10, pady=(0, 10))
        self.pool_result_text = tk.Text(result_frame, height=5, wrap=tk.WORD)
        self.pool_result_text.pack(fill=tk.X, padx=10, pady=10)
        self.pool_result_text.config(state=tk.DISABLED)

        self._update_pool_list()

    def _on_search(self, event=None):
        query = self.search_entry.get().strip()
        if not query:
            return

        self.search_listbox.delete(0, tk.END)
        self.search_listbox.config(height=5)

        def search_thread():
            results = self.stock_service.search_stocks(query)
            self.loading_queue.put(('search_results', results))

        threading.Thread(target=search_thread, daemon=True).start()

    def _on_search_select(self, event):
        selection = self.search_listbox.curselection()
        if not selection:
            return

        index = selection[0]
        stock = self.search_results[index]
        self._load_stock_data(stock.code)

    def _load_stock_data(self, code):
        self._clear_all_data()

        def load_thread():
            stock, financials = self.stock_service.get_stock_with_financials(code)
            self.loading_queue.put(('stock_data', stock, financials))

        threading.Thread(target=load_thread, daemon=True).start()

    def _clear_all_data(self):
        self.stock_info_text.config(state=tk.NORMAL)
        self.stock_info_text.delete(1.0, tk.END)
        self.stock_info_text.config(state=tk.DISABLED)

        for item in self.bs_tree.get_children():
            self.bs_tree.delete(item)
        for item in self.inc_tree.get_children():
            self.inc_tree.delete(item)
        for item in self.cf_tree.get_children():
            self.cf_tree.delete(item)

        for var in self.pe_vars.values():
            var.set("--")
        for var in self.pb_vars.values():
            var.set("--")

    def _start_loading_handler(self):
        def process_queue():
            while not self.loading_queue.empty():
                task = self.loading_queue.get()
                task_type = task[0]

                if task_type == 'search_results':
                    results = task[1]
                    self.search_results = results
                    self.search_listbox.delete(0, tk.END)
                    for stock in results:
                        self.search_listbox.insert(tk.END, f"{stock.code} - {stock.name}")
                    if not results:
                        self.search_listbox.insert(tk.END, "未找到匹配的股票")

                elif task_type == 'stock_data':
                    stock, financials = task[1], task[2]
                    self.current_stock = stock
                    self.current_financials = financials

                    if stock:
                        self._display_stock_info(stock)

                    if financials:
                        self._display_financials(financials)
                        self._calculate_valuation(stock, financials)

                    if not stock:
                        messagebox.showwarning("提示", "无法获取股票信息")

                elif task_type == 'pool_updated':
                    self._update_pool_list()

                elif task_type == 'pool_calculation':
                    pool_name, method_type, method, result, details = task[1], task[2], task[3], task[4], task[5]
                    self._display_pool_result(pool_name, method_type, method, result, details)

            self.after(100, process_queue)

        self.after(100, process_queue)

    def _display_stock_info(self, stock):
        self.stock_info_text.config(state=tk.NORMAL)
        self.stock_info_text.delete(1.0, tk.END)

        info = f"代码: {stock.code}\n"
        info += f"名称: {stock.name}\n"
        info += f"价格: {stock.price}\n" if stock.price else ""
        info += f"行业: {stock.industry}\n" if stock.industry else ""
        info += f"板块: {stock.sector}\n" if stock.sector else ""
        info += f"上市日期: {stock.list_date}\n" if stock.list_date else ""
        info += f"总市值: {stock.market_cap}\n" if stock.market_cap else ""
        info += f"PE(TTM): {stock.pe_ttm}\n" if stock.pe_ttm else ""
        info += f"PB: {stock.pb}\n" if stock.pb else ""

        self.stock_info_text.insert(tk.END, info)
        self.stock_info_text.config(state=tk.DISABLED)

    def _display_financials(self, financials):
        for item in self.bs_tree.get_children():
            self.bs_tree.delete(item)
        for bs in financials.balance_sheets:
            self.bs_tree.insert('', tk.END, values=(
                bs.report_date,
                bs.total_assets,
                bs.total_liabilities,
                bs.total_equity,
                bs.cash_and_equivalents
            ))

        for item in self.inc_tree.get_children():
            self.inc_tree.delete(item)
        for inc in financials.income_statements:
            self.inc_tree.insert('', tk.END, values=(
                inc.report_date,
                inc.revenue,
                inc.operating_profit,
                inc.net_profit,
                inc.net_profit_attributable,
                inc.eps
            ))

        for item in self.cf_tree.get_children():
            self.cf_tree.delete(item)
        for cf in financials.cash_flow_statements:
            self.cf_tree.insert('', tk.END, values=(
                cf.report_date,
                cf.operating_cash_flow,
                cf.investing_cash_flow,
                cf.financing_cash_flow,
                cf.net_cash_flow
            ))

    def _calculate_valuation(self, stock, financials):
        for key, var in self.pe_vars.items():
            pe = self.stock_service.calculate_pe(stock, financials, key)
            var.set(f"{pe:.2f}" if pe else "--")

        for key, var in self.pb_vars.items():
            pb = self.stock_service.calculate_pb(stock, financials, key)
            var.set(f"{pb:.2f}" if pb else "--")

    def _add_to_pool(self):
        if not self.current_stock:
            messagebox.showwarning("提示", "请先选择一只股票")
            return

        pools = self.pool_service.list_pools()
        if not pools:
            pool_name = simpledialog.askstring("新建股票池", "请输入股票池名称:")
            if pool_name:
                self.pool_service.create_pool(pool_name)
                pools = [pool_name]
            else:
                return

        pool_name = simpledialog.askstring("添加到股票池", "请输入股票池名称:", initialvalue=pools[0])
        if not pool_name:
            return

        if pool_name not in pools:
            self.pool_service.create_pool(pool_name)

        success = self.pool_service.add_stock_to_pool(pool_name, self.current_stock.code)
        if success:
            messagebox.showinfo("成功", f"股票已添加到股票池 '{pool_name}'")
            self.loading_queue.put(('pool_updated',))
        else:
            messagebox.showwarning("失败", "添加失败")

    def _create_pool(self):
        pool_name = simpledialog.askstring("新建股票池", "请输入股票池名称:")
        if pool_name:
            self.pool_service.create_pool(pool_name)
            self.loading_queue.put(('pool_updated',))

    def _delete_pool(self):
        pool_name = self.pool_var.get()
        if not pool_name:
            messagebox.showwarning("提示", "请先选择一个股票池")
            return

        if messagebox.askyesno("确认删除", f"确定要删除股票池 '{pool_name}' 吗?"):
            del self.pool_service.pools[pool_name]
            self.loading_queue.put(('pool_updated',))

    def _update_pool_list(self):
        pools = self.pool_service.list_pools()
        self.pool_combobox['values'] = pools

        for item in self.pool_tree.get_children():
            self.pool_tree.delete(item)

        if pools:
            pool_name = pools[0] if not self.pool_var.get() else self.pool_var.get()
            self.pool_var.set(pool_name)
            self._on_pool_select()

    def _on_pool_select(self, event=None):
        pool_name = self.pool_var.get()
        if not pool_name:
            return

        pool = self.pool_service.get_pool(pool_name)
        if not pool:
            return

        for item in self.pool_tree.get_children():
            self.pool_tree.delete(item)

        for stock in pool.stocks:
            self.pool_tree.insert('', tk.END, values=(stock.code, stock.name, stock.price))

    def _remove_from_pool(self):
        pool_name = self.pool_var.get()
        if not pool_name:
            messagebox.showwarning("提示", "请先选择一个股票池")
            return

        selection = self.pool_tree.selection()
        if not selection:
            messagebox.showwarning("提示", "请选择要移除的股票")
            return

        item = selection[0]
        values = self.pool_tree.item(item, 'values')
        stock_code = values[0]

        self.pool_service.remove_stock_from_pool(pool_name, stock_code)
        self.loading_queue.put(('pool_updated',))

    def _calculate_pool_pe(self):
        pool_name = self.pool_var.get()
        if not pool_name:
            messagebox.showwarning("提示", "请先选择一个股票池")
            return

        def calc_thread():
            for method, name in self.pe_methods.items():
                result = self.pool_service.calculate_pool_average_pe(pool_name, method)
                details = self.pool_service.get_pool_pe_details(pool_name, method)
                self.loading_queue.put(('pool_calculation', pool_name, 'PE', name, result, details))

        threading.Thread(target=calc_thread, daemon=True).start()

    def _calculate_pool_pb(self):
        pool_name = self.pool_var.get()
        if not pool_name:
            messagebox.showwarning("提示", "请先选择一个股票池")
            return

        def calc_thread():
            for method, name in self.pb_methods.items():
                result = self.pool_service.calculate_pool_average_pb(pool_name, method)
                details = self.pool_service.get_pool_pb_details(pool_name, method)
                self.loading_queue.put(('pool_calculation', pool_name, 'PB', name, result, details))

        threading.Thread(target=calc_thread, daemon=True).start()

    def _display_pool_result(self, pool_name, method_type, method, result, details):
        self.pool_result_text.config(state=tk.NORMAL)

        self.pool_result_text.insert(tk.END, f"股票池: {pool_name}\n")
        self.pool_result_text.insert(tk.END, f"{method_type}方法: {method}\n")
        self.pool_result_text.insert(tk.END, f"平均{method_type}: {result:.2f}\n" if result else f"平均{method_type}: 无法计算\n")
        self.pool_result_text.insert(tk.END, "明细:\n")

        for code, name, val in details:
            self.pool_result_text.insert(tk.END, f"  {code} - {name}: {val:.2f}\n" if val else f"  {code} - {name}: --\n")

        self.pool_result_text.insert(tk.END, "\n")
        self.pool_result_text.config(state=tk.DISABLED)


if __name__ == "__main__":
    app = StockApp()
    app.mainloop()