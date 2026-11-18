namespace CogniThink.TutorBrain.Tests;

using CogniThink.TutorBrain.Application.Chemistry;
using Xunit;

public sealed class ChemicalSimulationEngineTests
{
    // ==================== 置换与金属活动性 ====================

    [Fact]
    public void 钠与水反应生成氢氧化钠和氢气()
    {
        var result = Fixture.Simulate(new[] { ("Na", 2.0), ("H2O", 4.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-NA-H2O");
        Assert.Contains("NaOH", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains("H2", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains(result.FinalGases, g => g.Contains("氢气"));
        Assert.Contains(result.Phenomena, p => p.Contains("钠浮在水面"));
    }

    [Fact]
    public void 铁置换铜溶液由蓝色变浅绿色并析出红色固体()
    {
        var result = Fixture.Simulate(new[] { ("Fe", 1.0), ("CuSO4", 1.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-FE-CUSO4");
        Assert.Contains("FeSO4", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains("Cu", result.FinalSpecies.Select(s => s.Id));
        Assert.Equal("浅绿色", result.FinalSolutionColor);
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("铜"));
    }

    [Fact]
    public void 铁与盐酸生成浅绿色溶液和氢气()
    {
        var result = Fixture.Simulate(new[] { ("Fe", 1.0), ("HCl", 2.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-FE-HCL");
        Assert.Equal("浅绿色", result.FinalSolutionColor);
        Assert.Contains(result.FinalGases, g => g.Contains("氢气"));
    }

    [Fact]
    public void 铝与氢氧化钠溶液反应生成偏铝酸钠和氢气()
    {
        var result = Fixture.Simulate(new[] { ("Al", 2.0), ("NaOH", 2.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-AL-NAOH");
        Assert.Contains("NaAlO2", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains(result.FinalGases, g => g.Contains("氢气"));
    }

    [Fact]
    public void 氯气置换碘化钾生成碘单质()
    {
        var result = Fixture.Simulate(new[] { ("Cl2", 1.0), ("KI", 2.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CL2-KI");
        Assert.Contains("I2", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains(result.FinalSolids, s => s.Contains("碘"));
    }

    // ==================== 沉淀与颜色 ====================

    [Fact]
    public void 硫酸铜与氢氧化钠生成蓝色沉淀()
    {
        var result = Fixture.Simulate(new[] { ("CuSO4", 1.0), ("NaOH", 2.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CUSO4-NAOH");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("蓝色") && p.Contains("氢氧化铜"));
        Assert.Equal("无色", result.FinalSolutionColor);
    }

    [Fact]
    public void 氯化钡与硫酸钠生成白色硫酸钡沉淀()
    {
        var result = Fixture.Simulate(new[] { ("BaCl2", 1.0), ("Na2SO4", 1.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-BACL2-NA2SO4");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("白色") && p.Contains("硫酸钡"));
    }

    [Fact]
    public void 氯化铁与硫氰化钾溶液显血红色()
    {
        var result = Fixture.Simulate(new[] { ("FeCl3", 1.0), ("KSCN", 3.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-077");
        Assert.Equal("血红色", result.FinalSolutionColor);
        Assert.Contains(result.FinalSpecies, s => s.Id == "Fe(SCN)3");
    }

    [Fact]
    public void 氢氧化铁与盐酸溶解后溶液呈黄色()
    {
        var result = Fixture.Simulate(new[] { ("Fe(OH)3", 1.0), ("HCl", 3.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-FEOH3-HCL");
        Assert.Equal("黄色", result.FinalSolutionColor);
        Assert.DoesNotContain(result.FinalPrecipitates, p => p.Contains("氢氧化铁"));
    }

    // ==================== 连续反应：氢氧化亚铁被空气氧化 ====================

    [Fact]
    public void 氯化亚铁与氢氧化钠生成白色沉淀后被空气氧化为红褐色()
    {
        var result = Fixture.Simulate(new[] { ("FeCl2", 1.0), ("NaOH", 2.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-FECL2-NAOH");
        Assert.Contains(result.Events, e => e.ReactionId == "R-FEOH2-O2");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("红褐色") && p.Contains("氢氧化铁"));
        Assert.DoesNotContain(result.FinalPrecipitates, p => p.Contains("氢氧化亚铁"));
    }

    [Fact]
    public void 分阶段加入氢氧化钠先生成氯化铁再生成红褐色沉淀()
    {
        var request = new SimulationRequest
        {
            Aqueous = true,
            Air = true,
            Species = new List<SimulationSpeciesInput>
            {
                new() { Substance = "Fe2O3", Moles = 1.0 },
                new() { Substance = "HCl", Moles = 6.0 }
            }
        };

        request.Stages.Add(new SimulationStage
        {
            Name = "加入氢氧化钠",
            Species = new List<SimulationSpeciesInput>
            {
                new() { Substance = "NaOH", Moles = 6.0 }
            }
        });

        var result = ChemistryFixture.Simulator.Simulate(request);

        Assert.Contains(result.Events, e => e.ReactionId == "R-FE2O3-HCL" && e.StageIndex == 0);
        Assert.Contains(result.Events, e => e.ReactionId == "R-FECL3-NAOH" && e.StageIndex == 1);
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("红褐色") && p.Contains("氢氧化铁"));
    }

    [Fact]
    public void 强酸强碱混合优先发生中和反应()
    {
        var result = Fixture.Simulate(new[] { ("HCl", 1.0), ("NaOH", 1.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-151");
        Assert.Contains("NaCl", result.FinalSpecies.Select(s => s.Id));
        Assert.DoesNotContain(result.FinalSpecies, s => s.Id is "HCl" or "NaOH");
    }

    // ==================== 气体与分解 ====================

    [Fact]
    public void 碳酸钠与盐酸产生二氧化碳且气体不被水完全吸收()
    {
        var result = Fixture.Simulate(new[] { ("Na2CO3", 1.0), ("HCl", 2.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-NA2CO3-HCL");
        Assert.DoesNotContain(result.Events, e => e.ReactionId == "R-CO2-H2O");
        Assert.Contains(result.FinalGases, g => g.Contains("二氧化碳"));
    }

    [Fact]
    public void 碳酸氢钠受热分解生成碳酸钠和二氧化碳()
    {
        var result = Fixture.Simulate(new[] { ("NaHCO3", 2.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-NAHCO3-HEAT");
        Assert.Contains("Na2CO3", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains(result.FinalGases, g => g.Contains("二氧化碳"));
    }

    [Fact]
    public void 碳酸钙高温分解生成氧化钙和二氧化碳()
    {
        var result = Fixture.Simulate(new[] { ("CaCO3", 1.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-046");
        Assert.Contains("CaO", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains(result.FinalGases, g => g.Contains("二氧化碳"));
    }

    [Fact]
    public void 氢氧化铜受热分解为黑色氧化铜()
    {
        var result = Fixture.Simulate(new[] { ("Cu(OH)2", 1.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CUOH2-HEAT");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("黑色") && p.Contains("氧化铜"));
    }

    [Fact]
    public void 氯化铵与氢氧化钠加热放出氨气()
    {
        var result = Fixture.Simulate(new[] { ("NH4Cl", 1.0), ("NaOH", 1.0) }, r =>
        {
            r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-NH4CL-NAOH");
        Assert.Contains(result.FinalGases, g => g.Contains("氨"));
    }

    // ==================== 条件守门 ====================

    [Fact]
    public void 未点燃时铁在空气中不会燃烧()
    {
        var result = Fixture.Simulate(new[] { ("Fe", 3.0) }, r =>
        {
            r.Aqueous = false;
        });

        Assert.DoesNotContain(result.Events, e => e.ReactionId is "R-FE-O2" or "R-FE-CL2");
        Assert.Contains(result.SkippedReactions, s => s.ReactionId == "R-FE-O2");
    }

    [Fact]
    public void 点燃条件下铁在氧气中燃烧生成四氧化三铁()
    {
        var result = Fixture.Simulate(new[] { ("Fe", 3.0), ("O2", 2.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Ignition = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-FE-O2");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("四氧化三铁"));
    }

    [Fact]
    public void 铝热反应需要高温和引燃剂()
    {
        var result = Fixture.Simulate(new[] { ("Al", 2.0), ("Fe2O3", 1.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Heating = true;
            r.Ignition = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-062");
        Assert.Contains("Fe", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains("Al2O3", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 铁与氯气点燃生成氯化铁()
    {
        var result = Fixture.Simulate(new[] { ("Fe", 2.0), ("Cl2", 3.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Ignition = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-FE-CL2");
        Assert.Contains("FeCl3", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 过氧化氢分解需要催化剂条件()
    {
        var without = Fixture.Simulate(new[] { ("H2O2", 2.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
        });

        Assert.DoesNotContain(without.Events, e => e.ReactionId == "R-140");

        var with = Fixture.Simulate(new[] { ("H2O2", 2.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Catalyst = true;
        });

        Assert.Contains(with.Events, e => e.ReactionId == "R-140");
        Assert.Contains(with.FinalGases, g => g.Contains("氧气"));
    }

    // ==================== 新增典型反应 ====================

    [Fact]
    public void 过氧化钠吸收二氧化碳放出氧气()
    {
        var result = Fixture.Simulate(new[] { ("Na2O2", 2.0), ("CO2", 2.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-NA2O2-CO2");
        Assert.Contains("Na2CO3", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains(result.FinalGases, g => g.Contains("氧气"));
    }

    [Fact]
    public void 碳酸氢钠与石灰水生成白色沉淀()
    {
        var result = Fixture.Simulate(new[] { ("NaHCO3", 1.0), ("Ca(OH)2", 1.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-NAHCO3-CAOH2");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("白色") && p.Contains("碳酸钙"));
    }

    [Fact]
    public void 高温下碳酸钠与二氧化硅反应生成硅酸钠()
    {
        var result = Fixture.Simulate(new[] { ("Na2CO3", 1.0), ("SiO2", 1.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-NA2CO3-SIO2");
        Assert.Contains("Na2SiO3", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains(result.FinalGases, g => g.Contains("二氧化碳"));
    }

    [Fact]
    public void 碳在氧气中燃烧生成二氧化碳()
    {
        var result = Fixture.Simulate(new[] { ("C", 1.0), ("O2", 1.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Ignition = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-C-O2");
        Assert.Contains("CO2", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 高温下二氧化碳与碳生成一氧化碳()
    {
        var result = Fixture.Simulate(new[] { ("CO2", 1.0), ("C", 1.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CO2-C");
        Assert.Contains("CO", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 电解水生成氢气和氧气()
    {
        var result = Fixture.Simulate(new[] { ("H2O", 2.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Electricity = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-H2O-ELECTROLYSIS");
        Assert.Contains("H2", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains("O2", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 电解硫酸铜溶液阴极析出铜()
    {
        var result = Fixture.Simulate(new[] { ("CuSO4", 2.0) }, r =>
        {
            r.Electricity = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CUSO4-ELECTROLYSIS");
        Assert.Contains("Cu", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains(result.FinalGases, g => g.Contains("氧气"));
    }

    [Fact]
    public void 氯气置换溴化钠中的溴()
    {
        var result = Fixture.Simulate(new[] { ("NaBr", 2.0), ("Cl2", 1.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-NABR-CL2");
        Assert.Contains("Br2", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 铁与稀硫酸反应生成氢气()
    {
        var result = Fixture.Simulate(new[] { ("Fe", 1.0), ("H2SO4", 1.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-FE-H2SO4");
        Assert.Contains(result.FinalGases, g => g.Contains("氢气"));
        Assert.Equal("浅绿色", result.FinalSolutionColor);
    }

    [Fact]
    public void 高温水蒸气与铁反应生成四氧化三铁()
    {
        var result = Fixture.Simulate(new[] { ("Fe", 3.0), ("H2O", 4.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-FE-H2O-G");
        Assert.Contains("Fe3O4", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains(result.FinalGases, g => g.Contains("氢气"));
    }

    [Fact]
    public void 氨气与硝酸反应生成硝酸铵()
    {
        var result = Fixture.Simulate(new[] { ("NH3", 1.0), ("HNO3", 1.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-NH3-HNO3");
        Assert.Contains("NH4NO3", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 乙酸与碳酸钠反应放出二氧化碳()
    {
        var result = Fixture.Simulate(new[] { ("CH3COOH", 2.0), ("Na2CO3", 1.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CH3COOH-NA2CO3");
        Assert.Contains(result.FinalGases, g => g.Contains("二氧化碳"));
    }

    [Fact]
    public void 锌置换硫酸铜中的铜()
    {
        var result = Fixture.Simulate(new[] { ("Zn", 1.0), ("CuSO4", 1.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-ZN-CUSO4");
        Assert.Contains("Cu", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains("ZnSO4", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 二氧化硫催化氧化生成三氧化硫()
    {
        var result = Fixture.Simulate(new[] { ("SO2", 2.0), ("O2", 1.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Heating = true;
            r.Catalyst = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-SO2-O2");
        Assert.Contains("SO3", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 高温下二氧化硅与碳酸钙生成硅酸钙()
    {
        var result = Fixture.Simulate(new[] { ("SiO2", 1.0), ("CaCO3", 1.0) }, r =>
        {
            r.Aqueous = false;
            r.Air = false;
            r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-SIO2-CACO3");
        Assert.Contains("CaSiO3", result.FinalSpecies.Select(s => s.Id));
    }

    // ==================== 限量试剂 ====================

    [Fact]
    public void 氢氧化钠不足时按系数比生成一半氢氧化铜()
    {
        var result = Fixture.Simulate(new[] { ("CuSO4", 1.0), ("NaOH", 1.0) });

        var precipitate = result.FinalSpecies.Single(s => s.Id == "Cu(OH)2");
        Assert.Equal(0.5, precipitate.FinalMoles, 3);

        // NaOH 为限量试剂被完全消耗，CuSO4 过量 0.5 mol。
        Assert.DoesNotContain(result.FinalSpecies, s => s.Id == "NaOH");
        Assert.Equal(0.5, result.FinalSpecies.Single(s => s.Id == "CuSO4").FinalMoles, 3);
    }

    [Fact]
    public void 氧化铁与足量盐酸按系数生成两摩尔氯化铁()
    {
        var result = Fixture.Simulate(new[] { ("Fe2O3", 1.0), ("HCl", 6.0) });

        var ferric = result.FinalSpecies.Single(s => s.Id == "FeCl3");
        Assert.Equal(2.0, ferric.FinalMoles, 3);
        Assert.DoesNotContain(result.FinalSpecies, s => s.Id is "Fe2O3" or "HCl");
    }

    // ==================== 过量/少量条件 ====================

    [Fact]
    public void 少量二氧化碳与氢氧化钠生成碳酸钠()
    {
        var result = Fixture.Simulate(new[] { ("NaOH", 2.0), ("CO2", 1.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-NAOH-CO2");
        Assert.Contains("Na2CO3", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 过量二氧化碳使石灰水先浑浊后澄清()
    {
        var result = Fixture.Simulate(new[] { ("Ca(OH)2", 1.0), ("CO2", 2.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CO2-CAOH2");
        Assert.Contains(result.Events, e => e.ReactionId == "R-047");
        Assert.Contains("Ca(HCO3)2", result.FinalSpecies.Select(s => s.Id));
        Assert.Empty(result.FinalPrecipitates);
    }

    // ==================== 新增无机反应 ====================

    [Fact]
    public void 铁在潮湿空气中生锈生成红棕色铁锈()
    {
        var result = Fixture.Simulate(new[] { ("Fe", 4.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-FE-RUST");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("铁锈"));
    }

    [Fact]
    public void 侯氏制碱生成碳酸氢钠和氯化铵()
    {
        var result = Fixture.Simulate(new[] { ("NaCl", 1.0), ("NH3", 1.0), ("CO2", 1.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-HOUBELL");
        Assert.Contains("NaHCO3", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains("NH4Cl", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 铁与稀硝酸生成一氧化氮和黄色溶液()
    {
        var result = Fixture.Simulate(new[] { ("Fe", 1.0), ("HNO3", 4.0) }, r => { r.Air = false; r.Dilute = true; });

        Assert.Contains(result.Events, e => e.ReactionId == "R-FE-HNO3-DILUTE");
        Assert.Contains("Fe(NO3)3", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains(result.FinalGases, g => g.Contains("一氧化氮"));
    }

    [Fact]
    public void 氧化铁与硫酸生成黄色硫酸铁溶液()
    {
        var result = Fixture.Simulate(new[] { ("Fe2O3", 1.0), ("H2SO4", 3.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-FE2O3-H2SO4");
        Assert.Equal("黄色", result.FinalSolutionColor);
    }

    [Fact]
    public void 四氧化三铁与盐酸同时生成氯化亚铁和氯化铁()
    {
        var result = Fixture.Simulate(new[] { ("Fe3O4", 1.0), ("HCl", 8.0) }, r => r.Air = false);

        Assert.Contains("FeCl2", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains("FeCl3", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 铜在潮湿空气中生成绿色铜绿()
    {
        var result = Fixture.Simulate(new[] { ("Cu", 2.0), ("CO2", 1.0) });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CU-O2-CO2-H2O");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("碱式碳酸铜"));
    }

    [Fact]
    public void 铜绿与盐酸生成蓝色溶液和二氧化碳()
    {
        var result = Fixture.Simulate(new[] { ("Cu2(OH)2CO3", 1.0), ("HCl", 4.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-CU2OH2CO3-HCL");
        Assert.Equal("蓝色", result.FinalSolutionColor);
        Assert.Contains(result.FinalGases, g => g.Contains("二氧化碳"));
    }

    [Fact]
    public void 铜绿受热分解为黑色氧化铜()
    {
        var result = Fixture.Simulate(new[] { ("Cu2(OH)2CO3", 1.0) }, r =>
        {
            r.Aqueous = false; r.Air = false; r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CU2OH2CO3-HEAT");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("黑色") && p.Contains("氧化铜"));
    }

    [Fact]
    public void 氯化铝与过量氢氧化钠生成偏铝酸钠()
    {
        var result = Fixture.Simulate(new[] { ("AlCl3", 1.0), ("NaOH", 4.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-ALCL3-NAOH-EXCESS");
        Assert.Contains("NaAlO2", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 氯化铝与碳酸氢钠双水解生成氢氧化铝和二氧化碳()
    {
        var result = Fixture.Simulate(new[] { ("AlCl3", 1.0), ("NaHCO3", 3.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-ALCL3-NAHCO3");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("氢氧化铝"));
        Assert.Contains(result.FinalGases, g => g.Contains("二氧化碳"));
    }

    [Fact]
    public void 氯化铁与碳酸氢钠双水解生成氢氧化铁和二氧化碳()
    {
        var result = Fixture.Simulate(new[] { ("FeCl3", 1.0), ("NaHCO3", 3.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-FECL3-NAHCO3");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("氢氧化铁"));
        Assert.Contains(result.FinalGases, g => g.Contains("二氧化碳"));
    }

    [Fact]
    public void 高锰酸钾与浓盐酸生成氯气()
    {
        var result = Fixture.Simulate(new[] { ("KMnO4", 2.0), ("HCl", 16.0) }, r => { r.Air = false; r.Concentrated = true; });

        Assert.Contains(result.Events, e => e.ReactionId == "R-KMNO4-HCL");
        Assert.Contains(result.FinalGases, g => g.Contains("氯气"));
    }

    [Fact]
    public void 高锰酸钾酸性氧化过氧化氢生成氧气()
    {
        var result = Fixture.Simulate(new[] { ("KMnO4", 2.0), ("H2O2", 5.0), ("H2SO4", 3.0) }, r => { r.Air = false; r.Acidic = true; });

        Assert.Contains(result.Events, e => e.ReactionId == "R-KMNO4-H2O2");
        Assert.Contains(result.FinalGases, g => g.Contains("氧气"));
    }

    [Fact]
    public void 次氯酸钠氧化碘化钾生成碘单质()
    {
        var result = Fixture.Simulate(new[] { ("NaClO", 1.0), ("KI", 2.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-NACLO-KI");
        Assert.Contains("I2", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 二氧化硫与硫化氢生成淡黄色硫()
    {
        var result = Fixture.Simulate(new[] { ("SO2", 1.0), ("H2S", 2.0) }, r => { r.Aqueous = false; r.Air = false; });

        // R-107 与 R-SO2-H2S 为同一反应的重复条目，编号靠前者触发
        Assert.Contains(result.Events, e => e.ReactionId == "R-107" || e.ReactionId == "R-SO2-H2S");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("硫"));
    }

    [Fact]
    public void 二氧化硅与氢氟酸生成四氟化硅气体()
    {
        var result = Fixture.Simulate(new[] { ("SiO2", 1.0), ("HF", 4.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-SIO2-HF");
        Assert.Contains(result.FinalGases, g => g.Contains("四氟化硅"));
    }

    [Fact]
    public void 硅与氢氧化钠溶液反应放出氢气()
    {
        var result = Fixture.Simulate(new[] { ("Si", 1.0), ("NaOH", 2.0) }, r => r.Air = false);

        Assert.Contains("Na2SiO3", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains(result.FinalGases, g => g.Contains("氢气"));
    }

    [Fact]
    public void 磷酸与足量氢氧化钠经三步中和生成磷酸钠()
    {
        var result = Fixture.Simulate(new[] { ("H3PO4", 1.0), ("NaOH", 3.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-H3PO4-NAOH-LOW");
        Assert.Contains(result.Events, e => e.ReactionId == "R-H3PO4-2NAOH");
        Assert.Contains(result.Events, e => e.ReactionId == "R-H3PO4-3NAOH");
        Assert.Contains("Na3PO4", result.FinalSpecies.Select(s => s.Id));
        Assert.DoesNotContain(result.FinalSpecies, s => s.Id == "H3PO4");
    }

    [Fact]
    public void 过量二氧化碳与氢氧化钠生成碳酸氢钠()
    {
        var result = Fixture.Simulate(new[] { ("NaOH", 1.0), ("CO2", 1.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-NAOH-CO2-EXCESS");
        Assert.Contains("NaHCO3", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 镁与热水反应生成氢气和氢氧化镁()
    {
        var result = Fixture.Simulate(new[] { ("Mg", 1.0) }, r => r.Heating = true);

        Assert.Contains(result.Events, e => e.ReactionId == "R-MG-H2O-HEAT");
        Assert.Contains(result.FinalGases, g => g.Contains("氢气"));
    }

    [Fact]
    public void 硝酸铜受热分解生成氧化铜和二氧化氮()
    {
        var result = Fixture.Simulate(new[] { ("Cu(NO3)2", 2.0) }, r =>
        {
            r.Aqueous = false; r.Air = false; r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CUNO32-HEAT");
        Assert.Contains("CuO", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 硝酸钾受热分解生成氧气()
    {
        var result = Fixture.Simulate(new[] { ("KNO3", 2.0) }, r =>
        {
            r.Aqueous = false; r.Air = false; r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-KNO3-HEAT");
        Assert.Contains(result.FinalGases, g => g.Contains("氧气"));
    }

    [Fact]
    public void 铅蓄电池放电生成白色硫酸铅沉淀()
    {
        var result = Fixture.Simulate(new[] { ("Pb", 1.0), ("PbO2", 1.0), ("H2SO4", 2.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-LEAD-DISCHARGE");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("硫酸铅"));
    }

    [Fact]
    public void 通电时铅蓄电池充电反应被触发()
    {
        var result = Fixture.Simulate(new[] { ("PbSO4", 2.0) }, r => { r.Air = false; r.Electricity = true; });

        Assert.Contains(result.Events, e => e.ReactionId == "R-LEAD-CHARGE");
    }

    [Fact]
    public void 二氧化氮聚合为无色四氧化二氮()
    {
        var result = Fixture.Simulate(new[] { ("NO2", 2.0) }, r => { r.Aqueous = false; r.Air = false; });

        Assert.Contains(result.Events, e => e.ReactionId == "R-NO2-N2O4");
        Assert.Contains("N2O4", result.FinalSpecies.Select(s => s.Id));
    }

    // ==================== 新增有机反应 ====================

    [Fact]
    public void 蛋白质加热变性生成变性蛋白质()
    {
        var result = Fixture.Simulate(new[] { ("Protein", 1.0) }, r => { r.Air = false; r.Heating = true; });

        Assert.Contains(result.Events, e => e.ReactionId == "R-PROTEIN-DENATURE");
        Assert.Contains("DenaturedProtein", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 变性蛋白质在催化加热下水解为氨基酸()
    {
        var result = Fixture.Simulate(new[] { ("DenaturedProtein", 1.0) }, r =>
        {
            r.Air = false; r.Heating = true; r.Catalyst = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-PROTEIN-HYDROLYSIS");
        Assert.Contains("AminoAcid", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 乙烷点燃生成二氧化碳和水()
    {
        var result = Fixture.Simulate(new[] { ("C2H6", 2.0), ("O2", 7.0) }, r =>
        {
            r.Aqueous = false; r.Air = false; r.Ignition = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-ETHANE-BURN");
        Assert.Contains(result.FinalGases, g => g.Contains("二氧化碳"));
    }

    [Fact]
    public void 乙烷光照下与氯气发生取代反应()
    {
        var result = Fixture.Simulate(new[] { ("C2H6", 2.0), ("Cl2", 2.0) }, r =>
        {
            r.Aqueous = false; r.Air = false; r.Light = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-ETHANE-CL2");
        Assert.Contains("C2H5Cl", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 环己烷点燃生成二氧化碳()
    {
        var result = Fixture.Simulate(new[] { ("C6H12", 1.0), ("O2", 9.0) }, r =>
        {
            r.Aqueous = false; r.Air = false; r.Ignition = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CYCLOHEXANE-BURN");
        Assert.Contains(result.FinalGases, g => g.Contains("二氧化碳"));
    }

    [Fact]
    public void 氯乙烯加聚生成聚氯乙烯()
    {
        var result = Fixture.Simulate(new[] { ("C2H3Cl", 1.0) }, r =>
        {
            r.Aqueous = false; r.Air = false; r.Heating = true; r.Catalyst = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-VCM-POLYMER");
        Assert.Contains("(C2H3Cl)n", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 苯在浓硫酸浓硝酸加热下硝化生成硝基苯()
    {
        var result = Fixture.Simulate(new[] { ("C6H6", 1.0), ("HNO3", 1.0) }, r =>
        {
            r.Aqueous = false; r.Air = false; r.Heating = true; r.Concentrated = true;
        });

        Assert.Contains("C6H5NO2", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 苯在催化剂下与溴取代生成溴苯()
    {
        var result = Fixture.Simulate(new[] { ("C6H6", 1.0), ("Br2", 1.0) }, r =>
        {
            r.Aqueous = false; r.Air = false; r.Catalyst = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-BENZENE-BROMINATION");
        Assert.Contains("C6H5Br", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 苯酚与溴水生成白色三溴苯酚沉淀()
    {
        var result = Fixture.Simulate(new[] { ("C6H5OH", 1.0), ("Br2", 3.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-PHENOL-BROMINATION");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("三溴苯酚"));
    }

    [Fact]
    public void 苯酚与氯化铁溶液显紫色()
    {
        var result = Fixture.Simulate(new[] { ("C6H5OH", 3.0), ("FeCl3", 1.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-PHENOL-FECL3");
        Assert.Equal("紫色", result.FinalSolutionColor);
    }

    [Fact]
    public void 苯酚钠与二氧化碳生成苯酚()
    {
        var result = Fixture.Simulate(new[] { ("C6H5ONa", 1.0), ("CO2", 1.0) }, r => r.Air = false);

        Assert.Contains("C6H5OH", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 乙醇与钠反应生成乙醇钠和氢气()
    {
        var result = Fixture.Simulate(new[] { ("C2H5OH", 2.0), ("Na", 2.0) }, r =>
        {
            r.Aqueous = false; r.Air = false;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-C2H5OH-NA");
        Assert.Contains(result.FinalGases, g => g.Contains("氢气"));
    }

    [Fact]
    public void 浓硫酸加热下乙醇发生消去生成乙烯()
    {
        var result = Fixture.Simulate(new[] { ("C2H5OH", 2.0) }, r =>
        {
            r.Aqueous = false; r.Air = false; r.Heating = true; r.Concentrated = true;
        });

        // 引擎不区分温度，170℃ 消去优先于 140℃ 成醚，生成乙烯。
        Assert.Contains(result.Events, e => e.ReactionId == "R-C2H5OH-170");
        Assert.Contains(result.FinalGases, g => g.Contains("乙烯"));
    }

    [Fact]
    public void 乙烯在催化加热下水化生成乙醇()
    {
        var result = Fixture.Simulate(new[] { ("C2H4", 1.0) }, r =>
        {
            r.Air = false; r.Heating = true; r.Catalyst = true;
        });

        Assert.Contains("C2H5OH", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 乙炔催化加氢生成乙烯()
    {
        var result = Fixture.Simulate(new[] { ("C2H2", 1.0), ("H2", 1.0) }, r =>
        {
            r.Aqueous = false; r.Air = false; r.Heating = true; r.Catalyst = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-C2H2-H2");
    }

    [Fact]
    public void 乙炔与溴加成生成四溴乙烷()
    {
        var result = Fixture.Simulate(new[] { ("C2H2", 1.0), ("Br2", 2.0) }, r =>
        {
            r.Aqueous = false; r.Air = false;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-C2H2-BR2");
        Assert.Contains("C2H2Br4", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 乙炔催化水化生成乙醛()
    {
        var result = Fixture.Simulate(new[] { ("C2H2", 1.0) }, r =>
        {
            r.Air = false; r.Heating = true; r.Catalyst = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-C2H2-H2O");
        Assert.Contains("CH3CHO", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 电石与水反应生成乙炔()
    {
        var result = Fixture.Simulate(new[] { ("CaC2", 1.0) }, r => r.Air = false);

        Assert.Contains(result.Events, e => e.ReactionId == "R-CAC2-H2O");
        Assert.Contains(result.FinalGases, g => g.Contains("乙炔"));
    }

    [Fact]
    public void 乙醛与新制氢氧化铜生成砖红色氧化亚铜()
    {
        var result = Fixture.Simulate(new[] { ("CH3CHO", 1.0), ("Cu(OH)2", 2.0) }, r =>
        {
            r.Air = false; r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-CH3CHO-CUOH2");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("砖红色") && p.Contains("氧化亚铜"));
    }

    [Fact]
    public void 葡萄糖与银氨溶液加热发生银镜反应()
    {
        var result = Fixture.Simulate(new[] { ("C6H12O6", 1.0), ("Ag(NH3)2OH", 2.0) }, r =>
        {
            r.Air = false; r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-GLUCOSE-AGNH3");
        Assert.Contains("Ag", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 葡萄糖与氢氧化铜加热生成砖红色氧化亚铜()
    {
        var result = Fixture.Simulate(new[] { ("C6H12O6", 1.0), ("Cu(OH)2", 2.0) }, r =>
        {
            r.Air = false; r.Heating = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-GLUCOSE-CUOH2");
        Assert.Contains(result.FinalPrecipitates, p => p.Contains("氧化亚铜"));
    }

    [Fact]
    public void 乙酸乙酯在碱性加热下皂化生成乙酸钠和乙醇()
    {
        var result = Fixture.Simulate(new[] { ("CH3COOC2H5", 1.0), ("NaOH", 1.0) }, r =>
        {
            r.Air = false; r.Heating = true; r.Alkaline = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-ESTER-SAPONIFICATION");
        Assert.Contains("CH3COONa", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains("C2H5OH", result.FinalSpecies.Select(s => s.Id));
    }

    [Fact]
    public void 油脂在碱性加热下皂化生成硬脂酸钠和甘油()
    {
        var result = Fixture.Simulate(new[] { ("(C17H35COO)3C3H5", 1.0), ("NaOH", 3.0) }, r =>
        {
            r.Air = false; r.Heating = true; r.Alkaline = true;
        });

        Assert.Contains(result.Events, e => e.ReactionId == "R-FAT-SAPONIFICATION");
        Assert.Contains("C17H35COONa", result.FinalSpecies.Select(s => s.Id));
        Assert.Contains("C3H8O3", result.FinalSpecies.Select(s => s.Id));
    }

    // ==================== 限量与分步中和 ====================

    [Fact]
    public void 氢氧化钠不足时磷酸只中和到磷酸二氢钠()
    {
        var result = Fixture.Simulate(new[] { ("H3PO4", 1.0), ("NaOH", 1.0) }, r => r.Air = false);

        Assert.Contains("NaH2PO4", result.FinalSpecies.Select(s => s.Id));
        Assert.DoesNotContain(result.FinalSpecies, s => s.Id == "Na2HPO4");
    }

    // ==================== 数据完整性 ====================

    [Fact]
    public void 全部反应方程式都能解析出计量系数()
    {
        foreach (var reaction in ChemistryFixture.Knowledge.Reactions)
            ChemistryFixture.Parser.Get(reaction);

        Assert.Empty(ChemistryFixture.Parser.Warnings);
        Assert.Empty(ChemistryFixture.Knowledge.LoadErrors);
    }

    private static class Fixture
    {
        public static SimulationResult Simulate(
            (string Substance, double Moles)[] species,
            Action<SimulationRequest>? configure = null) =>
            ChemistryFixture.Simulate(species, configure);
    }
}
