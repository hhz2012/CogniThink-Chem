using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// batch3 choice 子集测试：直接调用 ConceptChoiceValidator.TryDecide 断言。
/// 真值以题库 answer + 化学核对得到，集中在 ConceptTruth 字典里（单一事实源）。
/// 红线：零假阳性——校验器对“未列入真值表”的题返回非 null 即判失败；
/// 且不得与 Experiment/Organic/Structure 校验器在同一题上同时断言（路由无二义）；
/// 凡给出结论（含字母→被采纳门采纳）必被真实 IsAnswerCorrect 判为正确（零 harm）。
/// 扫描范围：GatewayVsLlmEvaluationService.BuildTestCases() 全题库（与生产完全一致）。
/// </summary>
public class Batch3ChoiceValidatorTests
{
    private static readonly List<QaTestCase> AllCases = GatewayVsLlmEvaluationService.BuildTestCases();

    public static readonly IReadOnlyDictionary<string, string> ConceptTruth = new Dictionary<string, string>
    {
        ["pdf-004"] = "B",
        ["pdf-118"] = "A",
        ["pdf-245"] = "C",
        ["pdf-372"] = "A",
        ["pdf-396"] = "B",
        ["pdf-442"] = "D",
        ["pdf-451"] = "C",
        ["pdf-493"] = "B",
        ["pdf-496"] = "D",
        ["pdf-497"] = "C",

        // batch3 扩展（第二批）：逐题化学核对 + 题库 answer 一致，零冲突
        ["pdf-034"] = "C",
        ["pdf-052"] = "B",
        ["pdf-086"] = "B",
        ["pdf-102"] = "B",
        ["pdf-129"] = "B",
        ["pdf-343"] = "B",
        ["pdf-407"] = "A",
        ["pdf-411"] = "D",
        ["pdf-440"] = "C",
        ["pdf-446"] = "B",
        ["pdf-503"] = "B",

        // batch3 扩展（第三批）：逐题化学核对 + 题库 answer 一致，零冲突
        // 6 道离子方程式正误判断（IonicEquationValidator 当前无解，由 Concept 事实库兜底判定）
        ["pdf-039"] = "A",
        ["pdf-040"] = "B",
        ["pdf-043"] = "B",
        ["pdf-047"] = "B",
        ["pdf-048"] = "C",
        ["pdf-050"] = "C",
        ["pdf-056"] = "B",
        // 4 道定性判断（化工流程 / 实验结论 / 化学用语）
        ["pdf-157"] = "C",
        ["pdf-339"] = "C",
        ["pdf-435"] = "B",

        // batch3 扩展（第四批）：氧化还原 / 热力学 / 离子平衡 / 化学用语 / 化学与生活定性题，逐题化学核对 + 题库 answer 一致，零冲突。
        // 净新增解出 7 道；pdf-110 经核对在加入事实前已被链路中非 Concept 校验器解出（ChoiceVerifier），Concept 事实冗余，故不纳入本批。
        ["pdf-081"] = "B",
        ["pdf-253"] = "A",
        ["pdf-282"] = "D",
        ["pdf-428"] = "B",
        ["pdf-429"] = "A",
        ["pdf-439"] = "C",
        ["pdf-445"] = "A",

        // batch3 扩展（第五批 / Task 35）：定性题，逐题化学核对 + 题库 answer 一致，零冲突。
        // 依赖 WrongDir 扩展「不属于 / 不合理」后 ask 方向判正（pdf-066 选非、pdf-453 选不合理）。
        ["pdf-066"] = "C",
        ["pdf-109"] = "A",
        ["pdf-285"] = "A",
        ["pdf-334"] = "C",
        ["pdf-402"] = "C",
        ["pdf-453"] = "B",

        // batch3 扩展（第六批 / Task 35）：实验现象 / 超分子 / 冠醚 / 精油结构定性题，逐题化学核对 + 题库 answer 一致，零冲突。
        ["pdf-401"] = "B",
        ["pdf-224"] = "D",
        ["pdf-342"] = "C",
        ["pdf-345"] = "B",

        // batch3 扩展（第七批 / Task 35）：元素推断题，逐题化学核对 + 题库 answer 一致，零冲突。
        ["pdf-229"] = "D",
        ["pdf-231"] = "B",
        ["pdf-234"] = "A",
        ["pdf-235"] = "A",
        ["pdf-236"] = "B",

        // batch3 扩展（第八批 / Task 35）：离子共存 / 实验结论 / 化工流程 / 核反应元素推断 / 分子结构定性题，逐题化学核对 + 题库 answer 一致，零冲突。
        ["pdf-060"] = "D",
        ["pdf-076"] = "C",
        ["pdf-112"] = "C",
        ["pdf-182"] = "A",
        ["pdf-457"] = "D",
        ["pdf-504"] = "C",

        // batch3 扩展（第九批 / Task 35 续）：先修正题库 answer 与化学真值冲突的 7 道，再策展进 Concept。ask=correct 取 verdict==true，ask=wrong 取 verdict==false，逐题独立核对化学真值。
        ["pdf-083"] = "D",
        ["pdf-092"] = "B",
        ["pdf-193"] = "C",
        ["pdf-194"] = "C",
        ["pdf-365"] = "A",
        ["pdf-368"] = "A",
        ["pdf-454"] = "B",

        // batch3 扩展（第十批 / Task 36）：全量审计后策展红线内可安全解的 10 道（含 3 道 answer 冲突修正：pdf-017/045/085）。
        ["pdf-017"] = "D",
        ["pdf-041"] = "A",
        ["pdf-042"] = "B",
        ["pdf-045"] = "C",
        ["pdf-049"] = "D",
        ["pdf-085"] = "D",
        ["pdf-110"] = "C",
        ["pdf-293"] = "C",
        ["pdf-296"] = "C",
        ["pdf-476"] = "D",

        // batch3 扩展（第十一批 / Task 36 续）：全量审计剩余候选中纯文本、定性、note 与 answer 一致的 3 道。
        ["pdf-073"] = "C",
        ["pdf-267"] = "C",
        ["pdf-283"] = "B",

        // batch3 扩展（第十二批 / Task 36 续）：pdf-067 题干已给配平方程式，4 选项 verdict 由化学唯一确定（note 写「C正确」有误，以 answer=C 与化学核对为准）。
        ["pdf-067"] = "C",

        // batch3 扩展（第十三批 / Task 37）：3 道答案矛盾题经官方题源核对后修正并策展。
        // pdf-116（2025 黑吉辽蒙）：答案 A 正确，但选项「解释」方程式列在 OCR 抽取时丢失，已补全；A 的解释方程式配平错误（生成 Cu2O 应为 Cu）。
        // pdf-305（2025 新课标）：存储 answer=A 错误，官方答案为 D（1 mol NO3- 得 8e-，对应 2 mol 而非 4 mol 羟基乙酸盐）。
        // pdf-316（2024 浙江 1 月）：存储 answer=A 错误，官方答案为 C（正极反应应为水还原，NH2- 在水溶液中不能稳定存在）。
        ["pdf-116"] = "A",
        ["pdf-305"] = "D",
        ["pdf-316"] = "C",

        // batch3 扩展（第十四批 / Task 37 续）：全量审计剩余候选中纯文本、可化学唯一判定的 18 道。
        // 含 4 道氧化还原/定性（pdf-068/072/080/082）、11 道 NA 计算（NaChoiceValidator 因含「转移电子/水解」等选项返回 null，由 Concept 事实兜底）、
        // 以及 3 道先经官方题源核对修正存储 answer 后策展的题（pdf-195 D→C、pdf-545 A→B、pdf-474 A→D）。
        // ask=correct 取 verdict==true 项，ask=wrong 取 verdict==false 项；每题 4 选项 verdict 独立核对化学真值，与修正后 answer 一致，零冲突。
        ["pdf-068"] = "C",
        ["pdf-072"] = "D",
        ["pdf-080"] = "B",
        ["pdf-082"] = "D",
        ["pdf-026"] = "D",
        ["pdf-027"] = "B",
        ["pdf-093"] = "A",
        ["pdf-419"] = "D",
        ["pdf-420"] = "B",
        ["pdf-421"] = "B",
        ["pdf-422"] = "A",
        ["pdf-423"] = "A",
        ["pdf-425"] = "A",
        ["pdf-426"] = "D",
        ["pdf-427"] = "A",
        ["pdf-195"] = "C",
        ["pdf-545"] = "B",
        ["pdf-474"] = "D",

        // batch3 扩展（第十五批 / Task 37 续）：全链路 dump 后从 235 道未解中筛出的纯文本、可化学唯一判定题。
        // pdf-184（核反应守恒）、pdf-294（Ksp 沉淀转化，note 确认）、pdf-028（差量法气体计算）。
        // 舍弃 pdf-386（note 自相矛盾、数据损坏）；pdf-075 原为存储选项错乱，已恢复原书选项（A/C 真、仅 B 假），
        // 改由单选择陈述路径（氧还电子计数 + 氧还反应计数 + 锚定事实）解出 B，见 redox-statement-judge-pdf075；
        // 其余候选多为图像/结构缺失或选项文本截断，无法零风险策展。
        // 注（image-ocr-choice-coverage 数据修复后）：pdf-006 / pdf-424 原在截断数据（仅 A/B）下可被 Concept
        // 唯一求解；后从源 PDF 恢复完整 C/D 选项，Concept 契约要求四选项全部命中事实（每选项恰好 1 条），
        // C/D 无对应事实 → 诚实返回 null，故一度从本表移除（计数 131→129）。
        // 续（本迭代 quick win）：为 pdf-006 补 GC-522-C/D（C 错因 C₆₀ 内含共价键、D 错因 K 属长周期），
        // 四选项 verdict 唯一指向 B，pdf-006 恢复可解（129→130）；为 pdf-424 补 GC-525-NA-C/D
        // （C 错因 1 mol 气体分子数必为 N_A 而非 3N_A、D 错因未注明标况致 11.2 L 不可换算），
        // 四选项 verdict 唯一指向 A，pdf-424 恢复可解（130→131）。
        ["pdf-006"] = "B",
        ["pdf-424"] = "A",
        ["pdf-184"] = "B",
        ["pdf-294"] = "B",
        ["pdf-028"] = "C",

        // batch3 扩展（第十六批 / Task 37 续 第三批）：从文本安全候选中策展的 11 道纯文本、可化学唯一判定题。
        // pdf-123（青花瓷成分·正确）/135（硫的性质·不正确）/136（物质处理方法·正确）/137（C/S 非金属性·正确）
        // /142（通 H2S 离子方程式·存在错误）/144（含硫转化一步实现·正确）/187（Sr/O 同位素·不正确）
        // /189（石墨烯硅烯锗烯·正确）/078（海带提碘·正确）/114（铜冶金污水·错误）/393（苯并呋喃同分异构·正确）。
        // pdf-393 原因「6种」是「16种」子串导致 D 选项串入 A 的事实而弃判；FactChoiceMatcher.OptContains
        //   新增数字前边界后该子串碰撞消除，4 选项各命中唯一事实，verdict 唯一指向 B（三取代苯 10 种扣除 H 得 9 种）。
        // 每题 4 选项 verdict 独立核算化学真值、与存储 answer 一致；stemMatch 均非空。
        ["pdf-123"] = "C",
        ["pdf-135"] = "C",
        ["pdf-136"] = "A",
        ["pdf-137"] = "D",
        ["pdf-142"] = "B",
        ["pdf-144"] = "D",
        ["pdf-187"] = "C",
        ["pdf-189"] = "A",
        ["pdf-078"] = "B",
        ["pdf-114"] = "D",
        ["pdf-393"] = "B",
        // batch3 扩展（Task 37 续 第四批）：全量审计未解集中纯文本、可化学唯一判定的 3 道定量题。
        // pdf-029（Zn+浓硫酸，气体 0.8 mol，错误项=A 因 Zn 实为 52.0 g 而非 48.0 g）、
        // pdf-079（Cl2+NaOH 加热共存体系，正确项=C 因转移电子 0.15<n<0.25）、
        // pdf-254（(CH3)2CHOH 气相分解平衡，正确项=A 因 Q=0.35>K≈0.27 逆向 v正<v逆）。
        // 每题 4 选项 verdict 独立核算化学真值、与存储 answer 一致；stemMatch 均非空且唯一。
        ["pdf-029"] = "A",
        ["pdf-079"] = "C",
        ["pdf-254"] = "A",
        // batch2（Task 40 P3）：纯文本选择题事实求解，4 选项纯文字可匹配、备注核真、stemMatch 限定题干。
        // pdf-321（有机命名，错误项=D）、pdf-378（聚碳酸异山梨醇酯官能团，错误项=B）、
        // pdf-380（聚碳酸酯 G，错误项=D）、pdf-350（蛇抱菌素，正确项=D）、
        // pdf-352（氘代甲醛加成，错误项=A）、pdf-358（6-硝基胡椒酸 X/Y，正确项=A）、
        // pdf-525（C—O/C—Br 键能，正确项=D）。
        ["pdf-321"] = "D",
        ["pdf-378"] = "B",
        ["pdf-380"] = "D",
        ["pdf-350"] = "D",
        ["pdf-352"] = "A",
        ["pdf-358"] = "A",
        ["pdf-525"] = "D",
        // batch3（Task 40 P3 收尾）：剩余纯文本选择题事实策展，4 选项纯文字可匹配、备注/答案键核真、stemMatch 全库唯一。
        // pdf-105（Fe 化合物实验，正确项=D）、pdf-106（FeSO4 性质探究目的，正确项=B）、
        // pdf-095（劳动项目与化学知识没有关联，错误项=A）、pdf-403（事实→结论，正确项=B）。
        ["pdf-105"] = "D",
        ["pdf-106"] = "B",
        ["pdf-095"] = "A",
        ["pdf-403"] = "B",
        // quick win 迭代：从 92 道纯文本零命中题中策展，每题 4 选项各补 1 条事实，化学核真、stemMatch 全库唯一。
        // pdf-101（KSCN 区分 Fe₂(SO₄)₃ / K₃[Fe(CN)₆]，正确项=C）、
        // pdf-269（NaHCO₃ 水解/电离平衡，错误项=B）、pdf-279（Na₂SO₃ / NaHSO₃，错误项=D）。计数 131→134。
        ["pdf-101"] = "C",
        ["pdf-269"] = "B",
        ["pdf-279"] = "D",
        // quick win 迭代第二批：pdf-211（(CF)x 结构性质，正确项=B）、
        // pdf-246（苯硝化能量图，错误项=C）、pdf-185（⁴⁰K 核素，错误项=D）。计数 134→137。
        ["pdf-211"] = "B",
        ["pdf-246"] = "C",
        ["pdf-185"] = "D",
        // quick win 迭代第三批：pdf-367（蛋白质/核酸，错误项=B）、
        // pdf-399（实验操作不合理，错误项=D）、pdf-505（化合物 J 元素推断，错误项=B）。计数 137→140。
        ["pdf-367"] = "B",
        ["pdf-399"] = "D",
        ["pdf-505"] = "B",
        // quick win 迭代 pdf-302：室温钠-硫电池（错误项=A），D 复用有机库 OF-449-C，
        // Concept 库补 GC-550-A/B/C/D 四选项事实；stemMatch「室温钠-硫电池」全库唯一。计数 140→141。
        ["pdf-302"] = "A",
        // 攻 SVG 硬骨头批次1：图非必需、4 选项文字可判，stemMatch 锁域补事实。
        // pdf-088（燃煤烟气脱硫，错误项=D）、pdf-132（单质M转化，错误项=C）、pdf-151（制硝酸流程，正确项=C）。计数 141→144。
        ["pdf-088"] = "D",
        ["pdf-132"] = "C",
        ["pdf-151"] = "C",
        // 攻 SVG 硬骨头批次2：图非必需、4 选项文字可判，stemMatch 锁域补事实。
        // pdf-018（钠及其化合物转化，错误项=D）、pdf-152（含氮价类图，正确项=C）、pdf-212（短周期元素推断，正确项=D）。计数 144→147。
        ["pdf-018"] = "D",
        ["pdf-152"] = "C",
        ["pdf-212"] = "D",
        // 攻 SVG 硬骨头批次3：图非必需、4 选项文字可判，stemMatch 锁域补事实。
        // pdf-103（Mg/Al/Fe 价类图，正确项=B）、pdf-130（含氯价类图方程式，正确项=D）、pdf-226（环六糊精超分子，错误项=B）。计数 147→150。
        ["pdf-103"] = "B",
        ["pdf-130"] = "D",
        ["pdf-226"] = "B",
        // 攻 SVG 硬骨头批次4：图非必需、4 选项文字可判，stemMatch 锁域补事实。
        // pdf-214（白磷/黑磷/磷化硼结构，正确项=B）、pdf-249（环氧丙烷反应机理，正确项=B）、pdf-479（含F/Cl 价类图，正确项=D）。计数 150→153。
        ["pdf-214"] = "B",
        ["pdf-249"] = "B",
        ["pdf-479"] = "D",
        // 攻 SVG 硬骨头批次5：图非必需、4 选项文字可判，stemMatch 锁域补事实。
        // pdf-125（Cl2/NaOH 转化，正确项=B）、pdf-223（18-冠-6 冠醚，正确项=D）、pdf-359（柳珊瑚酸，错误项=C）。计数 153→156。
        ["pdf-125"] = "B",
        ["pdf-223"] = "D",
        ["pdf-359"] = "C",
        // 纯文本可策展批次（无图、问法方向可判、stemMatch 全库唯一、四选项各恰好命中 1 条事实）：
        // pdf-148（盐酸/硫酸/硝酸用途，错误项=D）、pdf-158（回收镉锰工艺，错误项=D）、pdf-280（氨水与NH4Cl，不正确项=C）、
        // pdf-291（铜氨络离子生成，正确项=C）、pdf-384（PLGA 开环聚合，正确项=C）、pdf-349（乙酰氨基葡萄糖，正确项=B）。计数 156→162。
        ["pdf-148"] = "D",
        ["pdf-158"] = "D",
        ["pdf-280"] = "C",
        ["pdf-291"] = "C",
        ["pdf-384"] = "C",
        ["pdf-349"] = "B",
        // 攻 SVG 硬骨头批次6：图非必需、4 选项文字可判，stemMatch 锁域补事实。
        // pdf-361（华中五味子化合物L，错误项=C）、pdf-383（聚合物Z合成，正确项=B）、pdf-512（钒催化H2O2氧化苯，错误项=D）。计数 162→165。
        ["pdf-361"] = "C",
        ["pdf-383"] = "B",
        ["pdf-512"] = "D",
        // 攻 SVG/纯文本硬骨头批次7：图非必需或纯概念、4 选项文字可判，stemMatch 锁域补事实。
        // pdf-370（DNA片段，错误项=A）、pdf-381（化学与体育材料，正确项=D）、pdf-213（事实-解释配对，错误项=A）。计数 165→168。
        ["pdf-370"] = "A",
        ["pdf-381"] = "D",
        ["pdf-213"] = "A",
        // 纯文本兜底批次5：数据回填后 4 选项文字可唯一判定，stemMatch 锁域。pdf-436（化学用语错误项=C）、pdf-437（化学用语正确项=C）、pdf-233（元素推断正确项=B）。计数 171→174。
        ["pdf-436"] = "C",
        ["pdf-437"] = "C",
        ["pdf-233"] = "B",
        // 攻 SVG/纯文本硬骨头批次8：图非必需或纯概念、4 选项文字可判，stemMatch 锁域补事实。
        // pdf-494（催化剂，错误项=C）、pdf-363（天冬氨酸合成，正确项=C）、pdf-344（萘普生化合物Z，错误项=C）。计数 168→171。
        ["pdf-494"] = "C",
        ["pdf-363"] = "C",
        ["pdf-344"] = "C",
        // 攻 SVG/纯文本硬骨头批次9：图非必需或纯概念、4 选项文字可判，stemMatch 锁域补事实。
        // pdf-353（葫芦脲，错误项=A）、pdf-362（维生素A乙酸酯，正确项=C）、pdf-346（绿原酸，正确项=D）。计数 174→177。
        ["pdf-353"] = "A",
        ["pdf-362"] = "C",
        ["pdf-346"] = "D",
        // 攻 SVG/纯文本硬骨头批次10：图非必需或纯概念、4 选项文字可判，stemMatch 锁域补事实。
        // pdf-331（1,1-联环戊烯，错误项=A）、pdf-341（丙烷卤代选择性，正确项=D）、pdf-364（青蒿素，正确项=D）。计数 177→180。
        ["pdf-331"] = "A",
        ["pdf-341"] = "D",
        ["pdf-364"] = "D",
        // 攻 SVG/纯文本硬骨头批次11：图非必需或纯概念、4 选项文字可判，stemMatch 锁域补事实。
        // pdf-348（杯酚/C60，正确项=C）、pdf-351（甲基麦冬黄烷酮，正确项=D）、pdf-330（化合物A键线式，错误项=C）。计数 180→183。
        ["pdf-348"] = "C",
        ["pdf-351"] = "D",
        ["pdf-330"] = "C",
        // 攻 SVG/纯文本硬骨头批次12：题干自带反应式/数据、4选项纯文字可判，stemMatch 锁域补事实。
        // pdf-310（光催化芬顿，错误项=C）、pdf-288（制备MnCO3，正确项=C）、pdf-292（含镉废水处理，错误项=B）。计数 183→186。
        ["pdf-310"] = "C",
        ["pdf-288"] = "C",
        ["pdf-292"] = "B",
        // 本轮（pdf-237 答案键订正 B→A 后策展）：2024 重庆卷元素推断，R=H/X=B/Y=F/Z=N，问"正确的是"→正确项=A。
        // 4 选项各补 GC-700 事实（stemMatch 锁域），化学核真、stemMatch 全库唯一。计数 186→187。
        ["pdf-237"] = "A",
        // 攻 SVG/纯文本硬骨头批次13：4选项纯文字可判、stemMatch 锁域补事实。
        // pdf-113（浓硝酸与铜，错误项=C）、pdf-145（Na2S2O3与硫酸，错误项=C）。计数 187→189。
        ["pdf-113"] = "C",
        ["pdf-145"] = "C",
        // 攻 SVG/纯文本硬骨头批次14：4选项看图可判、stemMatch 锁域补事实。
        // pdf-250（X⇌Y⇌Z 能量图，正确项=B）、pdf-143（验证SO2性质装置，正确项=D）、pdf-128（氯气微型探究，正确项=D）。计数 189→192。
        ["pdf-250"] = "B",
        ["pdf-143"] = "D",
        ["pdf-128"] = "D",
        // 攻 SVG/纯文本硬骨头批次15：化学用语+实验装置图，4选项看图/文字可判、stemMatch 锁域补事实。
        // pdf-438（化学用语表示正确，正确项=B）、pdf-127（Cl2相关实验，正确项=C）、pdf-397（图示操作规范，正确项=A）。计数 192→195。
        ["pdf-438"] = "B",
        ["pdf-127"] = "C",
        ["pdf-397"] = "A",
        // 攻 SVG/纯文本硬骨头批次16：能量图+化学用语+实验装置，4选项看图/文字可判、stemMatch 锁域补事实。
        // pdf-238（能量图甲乙丙丁，正确项=C）、pdf-434（化学用语表示不正确，错误项=B）、pdf-150（NO装置不正确，错误项=D）。计数 195→198。
        ["pdf-238"] = "C",
        ["pdf-434"] = "B",
        ["pdf-150"] = "D",
        // 攻 SVG/纯文本硬骨头批次17：FeS2制绿矾+能量图(ΔH/键能)+Mo催化能量图，4选项看图/文字可判、stemMatch 锁域补事实。
        // pdf-104（不能达到目的，错误项=D）、pdf-239（能量图不正确，错误项=C）、pdf-247（Mo催化能量图不正确，错误项=C）。计数 198→201。
        ["pdf-104"] = "D",
        ["pdf-239"] = "C",
        ["pdf-247"] = "C",
        // 攻 SVG/纯文本硬骨头批次18：制肼装置+化学用语(s-pσ图)+NaCl Born-Haber循环，4选项看图/文字可判、stemMatch 锁域补事实。
        // pdf-153（制肼装置说法错误，错误项=A）、pdf-191（化学用语表达错误，错误项=A）、pdf-241（Born-Haber不正确，错误项=B）。计数 201→204。
        ["pdf-153"] = "A",
        ["pdf-191"] = "A",
        ["pdf-241"] = "B",
        // 攻 SVG/纯文本硬骨头批次19：离子转化氧化性图+Al海水电池+标准生成焓柱状图，4选项看图/文字可判、stemMatch 锁域补事实。
        // pdf-074（氧化性正确，正确项=A）、pdf-301（Al海水电池正确，正确项=A）、pdf-240（生成焓判断正确，正确项=C）。
        // 注：原候选 pdf-449（文房四宝错误，错误项=D）因与 OrganicChoiceValidator 路由重叠，Concept_NoOverlap 门失败，已回退该题（不入库）。计数 204→207。
        ["pdf-074"] = "A",
        ["pdf-301"] = "A",
        ["pdf-240"] = "C",
        // 攻 SVG/纯文本硬骨头批次20：Pt/Ag活性炭原电池+CO2加氢动力学曲线+甲醛-硝酸盐电池，4选项看图/文字可判、stemMatch 锁域补事实。
        // pdf-317（正确项=B）、pdf-248（错误项=D）、pdf-299（正确项=B）。计数 207→210。
        ["pdf-317"] = "B",
        ["pdf-248"] = "D",
        ["pdf-299"] = "B",
        // 攻 SVG/纯文本硬骨头批次21：BaSO4转化BaCO3溶解平衡曲线+Al-Ni-CuO可充电电池+锂-氮合成电池，4选项看图/文字可判、stemMatch 锁域补事实。
        // pdf-290（正确项=A）、pdf-297（错误项=A）、pdf-300（正确项=D）。计数 210→213。
        ["pdf-290"] = "A",
        ["pdf-297"] = "A",
        ["pdf-300"] = "D",
        // 攻 SVG/纯文本硬骨头批次22：Ca(OH)2/CaWO4 pc曲线+PbSO4/PbCO3溶解平衡曲线+CH3COONH4滴定曲线，4选项看图/文字可判、stemMatch 锁域补事实。
        // pdf-286（错误项=D）、pdf-287（错误项=D）、pdf-281（错误项=B）。计数 213→216。
        ["pdf-286"] = "D",
        ["pdf-287"] = "D",
        ["pdf-281"] = "B",
        // 攻 SVG 硬骨头批次23：Ba(OH)2/氨水稀释pH曲线+FeBr2通Cl2曲线+NaClO2制备工艺流程，4选项可判、stemMatch 锁域补事实。
        // pdf-265（错误项=A）、pdf-077（错误项=C）、pdf-126（正确项=B）。计数 216→219。
        ["pdf-265"] = "A",
        ["pdf-077"] = "C",
        ["pdf-126"] = "B",
        // 攻 SVG 硬骨头批次24：LiCoO2回收工艺流程（正确项=B）+1-苯基丙炔/HCl加成动力学曲线（错误项=C）。计数 219→221。
        ["pdf-111"] = "B",
        ["pdf-251"] = "C",
        // 攻 SVG 硬骨头批次25：CaF2萤石晶胞/HF·SiO2（正确项=D）+白铜Cu-Ni晶胞（正确项=A）+(Ba/K)BiO3钙钛矿（正确项=A）。计数 221→224。
        ["pdf-201"] = "D",
        ["pdf-221"] = "A",
        ["pdf-216"] = "A",
        // 攻 SVG 硬骨头批次26：化学用语(甘肃卷,错项=B)+铁系储氢合金晶胞(正确项=D)+化学用语(湖南卷,错项=C)。计数 224→227。
        ["pdf-433"] = "B",
        ["pdf-202"] = "D",
        ["pdf-431"] = "C",
        // 攻 SVG 硬骨头批次27：元素推断HSO3F(错项=A)+气体定律隔板(错项=C)+无机推断浓硫酸/C(正确项=A)。计数 227→230。
        ["pdf-232"] = "A",
        ["pdf-021"] = "C",
        ["pdf-141"] = "A",
        // 攻 SVG 硬骨头批次28：S+NaOH歧化/NA(错项=A)+铜价类图(正确项=D)+硫价类图(正确项=A)。计数 230→233。
        ["pdf-140"] = "A",
        ["pdf-014"] = "D",
        ["pdf-139"] = "A",
        // 攻 SVG 硬骨头批次29：醋酸甲酯制乙醇动力学(正确项=D)+Au-Cu合金晶胞(错项=C)+有机催化剂修饰色氨酸(正确项=C)。计数 233→236。
        ["pdf-256"] = "D",
        ["pdf-219"] = "C",
        ["pdf-371"] = "C",
        // 攻 SVG 硬骨头批次30：CaCO3/CaO晶胞(错项=D)+燃烧谱图推断X(正确项=D)+氧杂杯[4]芳烃合成(正确项=D)。计数 236→239。
        ["pdf-217"] = "D",
        ["pdf-390"] = "D",
        ["pdf-347"] = "D",
        // 批次31：O-乙酰基扁桃酰氯合成(错项=B)+实验装置不能达目的(错项=C)+实验装置使用正确(正确项=C)。计数 239→242。
        ["pdf-332"] = "B",
        ["pdf-400"] = "C",
        ["pdf-398"] = "C",
        // 批次32：FeSO4制备FeCO3流程(错项=B)+图示与结论相符(正确项=D)。计数 242→244。
        ["pdf-289"] = "B",
        ["pdf-037"] = "D",
        // 批次33：有机化合物结构(Ph-CH=CH-C(CH3)2-CH2Cl)共面/共线/等效氢/双键数(错项=B)。计数 244→245。
        ["pdf-387"] = "B",
        // choice-coverage-expansion（α 单选）：pdf-388（化合物M/抗生素中间体类似物·错项=D）、
        // pdf-394（四川南充·有机化合物说法正确的是·正确项=B）、pdf-459（制取BCl3装置能达目的·正确项=A）。
        // 事实写入 choice-curated-facts（GC-757~GC-759）；stemMatch 全库唯一，4 选项各命中唯一事实，
        // verdict 与官方 answer 一致；与 Experiment/Organic/Structure 无路由二义。计数 245→248。
        ["pdf-388"] = "D",
        ["pdf-394"] = "B",
        ["pdf-459"] = "A",
        // engine-capability-gap-fix T1：钠及其化合物转化/NA(正确项=D)+化学用语(正确项=C)。
        // 事实写入 choice-curated-facts（GC-762~GC-763）；探针 TOTAL PROBLEMS=0，
        // 4 选项各命中唯一事实，verdict 与官方 answer 一致，与 Experiment/Organic/Structure 无路由二义。计数 248→250。
        ["pdf-090"] = "D",
        ["pdf-188"] = "C",
        // svg_batch34：pdf-271（河北邢台二模·滴定曲线·错项=A）+ pdf-220（甘肃卷·β-MgCl2层状晶体·错项=D）。
        // 事实写入 general-choice-facts（GC-742~GC-743）；探针 TOTAL PROBLEMS=0，4 选项各命中唯一事实，
        // verdict 与官方 answer 一致，与 Experiment/Organic/Structure 无路由二义（screen_overlap clean）。
        ["pdf-271"] = "A",
        ["pdf-220"] = "D",
        // svg_batch35：pdf-218（湖南卷·Li2CN2晶胞·错项=D）+ pdf-357（贵州卷·灵芝酸B·错项=B）+ pdf-386（河北衡水期中·共面共线·错项=A）。
        // 事实写入 general-choice-facts（GC-744~GC-746）；探针 TOTAL PROBLEMS=0，4 选项各命中唯一事实，
        // verdict 与官方 answer 一致，与 Experiment/Organic/Structure 无路由二义（screen_overlap clean）。
        ["pdf-218"] = "D",
        ["pdf-357"] = "B",
        ["pdf-386"] = "A",
        // svg_batch36：pdf-337（河北承德期中·卤代烃消去+水解·正确项=C=②④⑥）。
        // 事实写入 general-choice-facts（GC-747）；探针 TOTAL PROBLEMS=0，4 选项各命中唯一事实，
        // verdict 与官方 answer 一致，与 Experiment/Organic/Structure 无路由二义（screen_overlap clean）。
        // 本批候选池 27 道中仅 pdf-337 答案键自洽可安全策展；其余 9 道答案键冲突、17 道属 playbook 不可解。
        ["pdf-337"] = "C",
        // keyfix_b37：用户授权修正答案键冲突的 7 道单选题（原 answer 与化学真值冲突，致 Concept 无法策展）。
        // 同步修正 pdf_questions.json 的 answer + answerKeywords，并补事实使 Concept 输出与新键一致。
        // 跳过 pdf-075（已恢复原书选项并由单选择陈述路径解出 B，不归 Concept 策展）与 pdf-325（A 选项结构图缺失，无法唯一判定）。
        ["pdf-087"] = "B",
        ["pdf-284"] = "B",
        ["pdf-295"] = "C",
        ["pdf-306"] = "B",
        ["pdf-410"] = "D",
        ["pdf-432"] = "A",
        ["pdf-511"] = "B",
        // keyfix_b38：用户授权修正答案键冲突的 3 道单选题（原 answer 与化学真值冲突，致 Concept 无法策展）。
        // 同步修正 pdf_questions.json 的 answer + answerKeywords，并补 12 条事实（GC-755~757）使 Concept 输出与新键一致。
        // pdf-227（原 A→B）、pdf-327（原 A→B）、pdf-409（原 D→C）；data-defect-register 交叉验证，探针 TOTAL PROBLEMS=0。
        ["pdf-227"] = "B",
        ["pdf-327"] = "B",
        ["pdf-409"] = "C"
    };

    public static IEnumerable<object[]> ConceptCases =>
        ConceptTruth.Select(kv => new object[] { kv.Key, kv.Value });

    [Theory]
    [MemberData(nameof(ConceptCases))]
    public void Concept_Solves_KnownQuestion(string id, string expected)
    {
        var r = ConceptChoiceValidator.TryDecide(Req(id));
        Assert.NotNull(r);
        Assert.Equal(expected, r!.AnswerLetter);
    }

    [Fact]
    public void Concept_ZeroFalsePositive_AcrossAllBuildTestCases()
    {
        foreach (var c in AllCases)
        {
            var r = ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (r is null) continue;
            Assert.True(ConceptTruth.TryGetValue(c.Id, out var exp),
                $"[零假阳性] ConceptChoiceValidator 对未列入真值表的题 {c.Id} 返回 {r.AnswerLetter}");
            Assert.Equal(exp, r.AnswerLetter);
        }
    }

    [Fact]
    public void Concept_Coverage_MatchesTruthTable()
    {
        var solved = AllCases.Count(c => ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }) != null);
        Assert.Equal(ConceptTruth.Count, solved);
    }

    [Fact]
    public void Concept_AdoptedAnswers_AreGradedCorrect_AcrossAllBuildTestCases()
    {
        // 红线：凡 ConceptChoiceValidator 给出结论（必含选项字母 → 被采纳门采纳），
        // 其结论必须被真实判分 IsAnswerCorrect 判为正确，否则即为 harm（采纳了错误答案）。
        foreach (var c in AllCases)
        {
            var r = ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (r is null) continue;
            Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                $"[harm] {c.Id} 结论 [{r.FinalResult}] 未被判为正确（type={c.Type}, answer=[{c.Answer}]）");
        }
    }

    [Fact]
    public void Concept_NoOverlap_WithExperimentOrganicAndStructure()
    {
        foreach (var c in AllCases)
        {
            var r = ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (r is null) continue;
            var e = ExperimentChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            var o = OrganicChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            var s = StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = c.Question });
            Assert.False(e != null,
                $"[路由二义] Concept 与 Experiment 同时断言 {c.Id}: con={r.AnswerLetter}, exp={e?.AnswerLetter}");
            Assert.False(o != null,
                $"[路由二义] Concept 与 Organic 同时断言 {c.Id}: con={r.AnswerLetter}, org={o?.AnswerLetter}");
            Assert.False(s != null,
                $"[路由二义] Concept 与 Structure 同时断言 {c.Id}: con={r.AnswerLetter}, str={s?.AnswerLetter}");
        }
    }

    private static EngineRequest Req(string id)
    {
        var c = AllCases.FirstOrDefault(x => x.Id == id);
        Assert.NotNull(c);
        return new EngineRequest { RawText = c!.Question };
    }
}
