import click
from tabulate import tabulate
from models import Stock, FinancialStatements
from data_access import AkShareDataSource, SQLiteCache, CachedDataSource
from business import StockService, StockPoolService


@click.group()
@click.pass_context
def cli(ctx):
    ctx.ensure_object(dict)
    cache = SQLiteCache()
    primary_source = AkShareDataSource()
    data_source = CachedDataSource(primary_source, cache)
    ctx.obj['stock_service'] = StockService(data_source)
    ctx.obj['pool_service'] = StockPoolService(ctx.obj['stock_service'])


@cli.command()
@click.argument('query')
@click.pass_context
def search(ctx, query):
    stock_service = ctx.obj['stock_service']
    results = stock_service.search_stocks(query)
    if not results:
        click.echo("未找到匹配的股票")
        return
    table_data = [[stock.code, stock.name] for stock in results]
    click.echo(tabulate(table_data, headers=['代码', '名称'], tablefmt='grid'))


@cli.command()
@click.argument('code')
@click.pass_context
def info(ctx, code):
    stock_service = ctx.obj['stock_service']
    stock, financials = stock_service.get_stock_with_financials(code)
    if not stock:
        click.echo(f"无法获取股票 {code} 的信息")
        return

    click.echo(f"\n股票信息:")
    click.echo(f"代码: {stock.code}")
    click.echo(f"名称: {stock.name}")
    click.echo(f"价格: {stock.price}" if stock.price else "")
    click.echo(f"行业: {stock.industry}" if stock.industry else "")
    click.echo(f"板块: {stock.sector}" if stock.sector else "")
    click.echo(f"上市日期: {stock.list_date}" if stock.list_date else "")
    click.echo(f"总市值: {stock.market_cap}" if stock.market_cap else "")

    if financials:
        click.echo("\n财务报表:")

        latest_bs = financials.get_latest_balance_sheet()
        if latest_bs:
            click.echo(f"\n最新资产负债表 ({latest_bs.report_date}):")
            click.echo(f"总资产: {latest_bs.total_assets}")
            click.echo(f"总负债: {latest_bs.total_liabilities}")
            click.echo(f"净资产: {latest_bs.total_equity}")

        latest_inc = financials.get_latest_income_statement()
        if latest_inc:
            click.echo(f"\n最新利润表 ({latest_inc.report_date}):")
            click.echo(f"营业收入: {latest_inc.revenue}")
            click.echo(f"净利润: {latest_inc.net_profit}")
            click.echo(f"归属母公司净利润: {latest_inc.net_profit_attributable}")

        ttm_profit = financials.get_ttm_net_profit()
        if ttm_profit:
            click.echo(f"\nTTM净利润: {ttm_profit}")


@cli.command()
@click.argument('code')
@click.option('--method', '-m', default='ttm', help='PE计算方法: ttm, static, dynamic, cash, debt')
@click.pass_context
def pe(ctx, code, method):
    stock_service = ctx.obj['stock_service']
    stock, financials = stock_service.get_stock_with_financials(code)
    if not stock:
        click.echo(f"无法获取股票 {code} 的信息")
        return
    if not financials:
        click.echo(f"无法获取股票 {code} 的财务数据")
        return

    pe_value = stock_service.calculate_pe(stock, financials, method)
    if pe_value:
        method_name = stock_service.get_all_pe_methods().get(method, method)
        click.echo(f"{stock.name} ({stock.code}) - {method_name}: {pe_value:.2f}")
    else:
        click.echo(f"无法计算 {method} PE")


@cli.command()
@click.argument('code')
@click.option('--method', '-m', default='basic', help='PB计算方法: basic, tangible, cash')
@click.pass_context
def pb(ctx, code, method):
    stock_service = ctx.obj['stock_service']
    stock, financials = stock_service.get_stock_with_financials(code)
    if not stock:
        click.echo(f"无法获取股票 {code} 的信息")
        return
    if not financials:
        click.echo(f"无法获取股票 {code} 的财务数据")
        return

    pb_value = stock_service.calculate_pb(stock, financials, method)
    if pb_value:
        method_name = stock_service.get_all_pb_methods().get(method, method)
        click.echo(f"{stock.name} ({stock.code}) - {method_name}: {pb_value:.2f}")
    else:
        click.echo(f"无法计算 {method} PB")


@cli.command()
@click.argument('code')
@click.pass_context
def ratios(ctx, code):
    stock_service = ctx.obj['stock_service']
    stock, financials = stock_service.get_stock_with_financials(code)
    if not stock:
        click.echo(f"无法获取股票 {code} 的信息")
        return
    if not financials:
        click.echo(f"无法获取股票 {code} 的财务数据")
        return

    click.echo(f"\n{stock.name} ({stock.code}) - 估值指标")
    click.echo("-" * 50)

    pe_methods = stock_service.get_all_pe_methods()
    click.echo("\nPE指标:")
    for key, name in pe_methods.items():
        value = stock_service.calculate_pe(stock, financials, key)
        if value:
            click.echo(f"  {name}: {value:.2f}")
        else:
            click.echo(f"  {name}: N/A")

    pb_methods = stock_service.get_all_pb_methods()
    click.echo("\nPB指标:")
    for key, name in pb_methods.items():
        value = stock_service.calculate_pb(stock, financials, key)
        if value:
            click.echo(f"  {name}: {value:.2f}")
        else:
            click.echo(f"  {name}: N/A")


@cli.command()
@click.argument('pool_name')
@click.pass_context
def create_pool(ctx, pool_name):
    pool_service = ctx.obj['pool_service']
    pool = pool_service.create_pool(pool_name)
    click.echo(f"股票池 '{pool_name}' 创建成功")


@cli.command()
@click.pass_context
def list_pools(ctx):
    pool_service = ctx.obj['pool_service']
    pools = pool_service.list_pools()
    if not pools:
        click.echo("没有创建任何股票池")
        return
    click.echo("股票池列表:")
    for pool_name in pools:
        pool = pool_service.get_pool(pool_name)
        click.echo(f"  {pool_name} ({len(pool.stocks)} 只股票)")


@cli.command()
@click.argument('pool_name')
@click.argument('stock_code')
@click.pass_context
def add_to_pool(ctx, pool_name, stock_code):
    pool_service = ctx.obj['pool_service']
    success = pool_service.add_stock_to_pool(pool_name, stock_code)
    if success:
        click.echo(f"股票 {stock_code} 已添加到股票池 '{pool_name}'")
    else:
        click.echo(f"添加失败，请检查股票代码是否正确")


@cli.command()
@click.argument('pool_name')
@click.argument('stock_code')
@click.pass_context
def remove_from_pool(ctx, pool_name, stock_code):
    pool_service = ctx.obj['pool_service']
    success = pool_service.remove_stock_from_pool(pool_name, stock_code)
    if success:
        click.echo(f"股票 {stock_code} 已从股票池 '{pool_name}' 移除")
    else:
        click.echo(f"移除失败，请检查股票池和股票代码是否正确")


@cli.command()
@click.argument('pool_name')
@click.option('--method', '-m', default='ttm', help='PE计算方法')
@click.pass_context
def pool_pe(ctx, pool_name, method):
    pool_service = ctx.obj['pool_service']
    stock_service = ctx.obj['stock_service']

    details = pool_service.get_pool_pe_details(pool_name, method)
    if not details:
        click.echo(f"股票池 '{pool_name}' 为空或不存在")
        return

    method_name = stock_service.get_all_pe_methods().get(method, method)
    avg_pe = pool_service.calculate_pool_average_pe(pool_name, method)

    table_data = []
    for code, name, pe in details:
        table_data.append([code, name, f"{pe:.2f}" if pe else "N/A"])

    click.echo(f"\n股票池 '{pool_name}' - {method_name}")
    click.echo(tabulate(table_data, headers=['代码', '名称', 'PE'], tablefmt='grid'))
    if avg_pe:
        click.echo(f"\n平均PE: {avg_pe:.2f}")


@cli.command()
@click.argument('pool_name')
@click.option('--method', '-m', default='basic', help='PB计算方法')
@click.pass_context
def pool_pb(ctx, pool_name, method):
    pool_service = ctx.obj['pool_service']
    stock_service = ctx.obj['stock_service']

    details = pool_service.get_pool_pb_details(pool_name, method)
    if not details:
        click.echo(f"股票池 '{pool_name}' 为空或不存在")
        return

    method_name = stock_service.get_all_pb_methods().get(method, method)
    avg_pb = pool_service.calculate_pool_average_pb(pool_name, method)

    table_data = []
    for code, name, pb in details:
        table_data.append([code, name, f"{pb:.2f}" if pb else "N/A"])

    click.echo(f"\n股票池 '{pool_name}' - {method_name}")
    click.echo(tabulate(table_data, headers=['代码', '名称', 'PB'], tablefmt='grid'))
    if avg_pb:
        click.echo(f"\n平均PB: {avg_pb:.2f}")


@cli.command()
@click.argument('pool_name')
@click.pass_context
def pool_info(ctx, pool_name):
    pool_service = ctx.obj['pool_service']
    pool = pool_service.get_pool(pool_name)
    if not pool:
        click.echo(f"股票池 '{pool_name}' 不存在")
        return

    click.echo(f"\n股票池 '{pool_name}' 包含 {len(pool.stocks)} 只股票:")
    table_data = [[stock.code, stock.name, stock.price] for stock in pool.stocks]
    click.echo(tabulate(table_data, headers=['代码', '名称', '最新价'], tablefmt='grid'))


if __name__ == '__main__':
    cli()