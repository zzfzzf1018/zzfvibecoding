"""估值与收益计算的纯函数实现，与界面无关，便于单独测试复用。"""

from dataclasses import dataclass


@dataclass
class DcfYear:
    year: int
    cash_flow: float
    discount_factor: float
    present_value: float


@dataclass
class DcfResult:
    rows: list[DcfYear]
    explicit_pv: float
    terminal_value: float | None
    terminal_pv: float | None
    total_pv: float


def dcf(
    current_cash_flow: float,
    discount_rate: float,
    growth_rate: float,
    years: int,
    terminal_growth: float | None = None,
) -> DcfResult:
    """现金流折现。利率均为小数形式（0.08 表示 8%）。

    terminal_growth 为 None 时只折现显式预测期，否则按永续增长模型追加终值。
    """
    if years < 1:
        raise ValueError("年限必须大于等于 1")
    if discount_rate <= -1:
        raise ValueError("折现率必须大于 -100%")
    if growth_rate <= -1:
        raise ValueError("增长率必须大于 -100%")

    rows: list[DcfYear] = []
    for year in range(1, years + 1):
        cash_flow = current_cash_flow * (1 + growth_rate) ** year
        discount_factor = 1 / (1 + discount_rate) ** year
        rows.append(DcfYear(year, cash_flow, discount_factor, cash_flow * discount_factor))

    explicit_pv = sum(row.present_value for row in rows)

    terminal_value = None
    terminal_pv = None
    if terminal_growth is not None:
        if terminal_growth >= discount_rate:
            raise ValueError("永续增长率必须小于折现率，否则终值无意义")
        last_cash_flow = rows[-1].cash_flow
        terminal_value = last_cash_flow * (1 + terminal_growth) / (discount_rate - terminal_growth)
        terminal_pv = terminal_value / (1 + discount_rate) ** years

    return DcfResult(
        rows=rows,
        explicit_pv=explicit_pv,
        terminal_value=terminal_value,
        terminal_pv=terminal_pv,
        total_pv=explicit_pv + (terminal_pv or 0.0),
    )


@dataclass
class CompoundYear:
    year: int
    begin_balance: float
    contribution: float
    interest: float
    end_balance: float


@dataclass
class CompoundResult:
    rows: list[CompoundYear]
    final_balance: float
    total_invested: float
    total_interest: float


def compound(
    principal: float,
    annual_rate: float,
    years: int,
    periods_per_year: int = 1,
    contribution_per_period: float = 0.0,
) -> CompoundResult:
    """复利终值。每期期末追加投入 contribution_per_period。"""
    if years < 1:
        raise ValueError("年限必须大于等于 1")
    if periods_per_year < 1:
        raise ValueError("每年计息期数必须大于等于 1")
    if annual_rate <= -1:
        raise ValueError("年利率必须大于 -100%")

    period_rate = annual_rate / periods_per_year
    balance = principal
    total_invested = principal
    rows: list[CompoundYear] = []

    for year in range(1, years + 1):
        begin_balance = balance
        year_contribution = 0.0
        year_interest = 0.0
        for _ in range(periods_per_year):
            interest = balance * period_rate
            balance += interest + contribution_per_period
            year_interest += interest
            year_contribution += contribution_per_period
        total_invested += year_contribution
        rows.append(CompoundYear(year, begin_balance, year_contribution, year_interest, balance))

    return CompoundResult(
        rows=rows,
        final_balance=balance,
        total_invested=total_invested,
        total_interest=balance - total_invested,
    )


@dataclass
class PeYear:
    year: int
    earnings_index: float
    dynamic_pe: float


@dataclass
class PeResult:
    rows: list[PeYear]
    exit_pe: float | None
    total_return: float | None
    annualized_return: float | None


def dynamic_pe(
    current_pe: float,
    growth_rate: float,
    years: int,
    exit_pe: float | None = None,
) -> PeResult:
    """动态市盈率与持有回报。

    动态 PE 假设股价不变、盈利按 growth_rate 增长；
    给定 exit_pe 时按「盈利增长 x 估值变化」推算总回报与年化回报。
    """
    if years < 1:
        raise ValueError("年限必须大于等于 1")
    if current_pe <= 0:
        raise ValueError("当前 PE 必须大于 0")
    if growth_rate <= -1:
        raise ValueError("增长率必须大于 -100%")
    if exit_pe is not None and exit_pe <= 0:
        raise ValueError("目标 PE 必须大于 0")

    rows: list[PeYear] = []
    for year in range(1, years + 1):
        earnings_index = (1 + growth_rate) ** year
        rows.append(PeYear(year, earnings_index, current_pe / earnings_index))

    total_return = None
    annualized_return = None
    if exit_pe is not None:
        total_return = (1 + growth_rate) ** years * exit_pe / current_pe - 1
        annualized_return = (1 + total_return) ** (1 / years) - 1

    return PeResult(
        rows=rows,
        exit_pe=exit_pe,
        total_return=total_return,
        annualized_return=annualized_return,
    )
