namespace CogniThink.Chem.Core.Benchmark;

/// <summary>类别元数据：中文名称（讲解/导出展示用）与题库文件名（加载用）。</summary>
public sealed record CategoryInfo(string Name, string FileName);

/// <summary>
/// 12 类别元数据的唯一维护点：<see cref="Explanation.Explainer.CategoryName"/> 与
/// <see cref="BenchmarkLoader.CategoryFiles"/> 均委托本表，避免两处各自维护造成漂移。
/// </summary>
public static class CategoryCatalog
{
    /// <summary>类别 → 元数据（按枚举声明顺序，与原两张表逐字一致）。</summary>
    public static readonly IReadOnlyDictionary<ProblemCategory, CategoryInfo> All =
        new Dictionary<ProblemCategory, CategoryInfo>
        {
            [ProblemCategory.ProductInference] = new("产物推断", "product-inference.json"),
            [ProblemCategory.SubstanceIdentification] = new("成分推断", "substance-identification.json"),
            [ProblemCategory.ProcessInference] = new("过程推断", "process-inference.json"),
            [ProblemCategory.IonCoexistence] = new("离子共存", "ion-coexistence.json"),
            [ProblemCategory.StoichiometryCalc] = new("定量计算", "stoichiometry.json"),
            [ProblemCategory.Electrochemistry] = new("电化学", "hard-electrochemistry.json"),
            [ProblemCategory.ChemicalEquilibrium] = new("化学平衡", "hard-equilibrium.json"),
            [ProblemCategory.AqueousIonEquilibrium] = new("水溶液离子平衡", "hard-aqueous-equilibrium.json"),
            [ProblemCategory.IndustrialProcess] = new("工业流程综合", "hard-process-flow.json"),
            [ProblemCategory.IonPowderIdentification] = new("离子粉末推断", "hard-ion-powder.json"),
            [ProblemCategory.OrganicInference] = new("有机推断", "hard-organic.json"),
            [ProblemCategory.ExperimentalInquiry] = new("实验探究", "hard-experiment.json")
        };
}
