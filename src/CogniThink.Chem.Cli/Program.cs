using CogniThink.Chem.Cli.Commands;
using CogniThink.Chem.Core;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length > 0 && args[0] == "--demo")
{
    DemoCommand.Run();
    return 0;
}

if (args.Length > 0 && args[0] == "--solve")
{
    return SolveCommand.Run(args.Length > 1 ? args[1] : null);
}

if (args.Length > 0 && args[0] == "--export-web")
{
    return ExportWebCommand.Run(args.Length > 1 ? args[1] : null);
}

if (args.Length > 0 && args[0] == "--verify-real")
{
    return VerifyRealCommand.Run();
}

Console.WriteLine($"{ModuleInfo.DisplayName} ({ModuleInfo.Name}) v{ModuleInfo.Version}");
Console.WriteLine("命令：--demo（统一校验演示）　--solve [题目ID]（解+讲+引导 演示，可指定全量基准题库中的单题）　--export-web [输出目录]（导出前端网页数据）　--verify-real（真题求解验证）");
return 0;