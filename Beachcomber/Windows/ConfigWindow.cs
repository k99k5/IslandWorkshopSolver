using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace Beachcomber.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    public ConfigWindow(Plugin plugin, string rootPath) : base(
        "Beachcomber Configuration")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Configuration = plugin.Configuration;
        Configuration.rootPath = rootPath;
    }

    public void Dispose() { }

    public override void Draw()
    {
        float itemWidth = ImGui.GetContentRegionAvail().X / 2;
        ImGui.PushItemWidth(itemWidth); 
        
        if (ImGui.CollapsingHeader("求解器设置", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int sugg = Configuration.suggestionsToShow;
            if (ImGui.InputInt("显示方案数量", ref sugg))
            {
                Configuration.suggestionsToShow = sugg;
                Configuration.Save();
            }
            ImGui.Spacing();
            float matWeight = Configuration.materialValue;
            if (ImGui.SliderFloat("材料权重", ref matWeight, 0.0f, 1.0f))
            {
                Configuration.materialValue = matWeight;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Material weight is how much you care about the sale price of rare mats." +
                    "\n1 means \"I sell all excess mats and only care about total cowries.\"" +
                    "\n0 means \"I only care about getting the highest workshop revenue.\"" +
                    "\n0.5 is a nice balance. Ctrl + click to type an exact value");
            }
            ImGui.Spacing();
            bool showNet = Configuration.showNetCowries;
            if (ImGui.Checkbox("显示净利润", ref showNet))
            {
                Configuration.showNetCowries = showNet;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("净利润是工坊收入减去你直接出口稀有材料所获得的价值");
            }
            ImGui.Spacing();
            bool checkMats = Configuration.onlySuggestMaterialsOwned;
            if (ImGui.Checkbox("必须有稀有材料", ref checkMats))
            {
                Configuration.onlySuggestMaterialsOwned = checkMats;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("移除所有建议的时间表，这些时间表需要的稀有材料比你的库存中的多。");
            }
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("无人岛设置", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.PopItemWidth();
            ImGui.TextWrapped("These should automatically populate from your island sanctuary, but if you want to play around with them, here you go");
            ImGui.Spacing();
            ImGui.PushItemWidth(itemWidth);
            int rank = Configuration.islandRank;
            if (ImGui.InputInt("无人岛等级", ref rank))
            {
                Configuration.islandRank = rank;
                Configuration.Save();
            }
            ImGui.Spacing();
            int workshops = Configuration.numWorkshops;
            if (ImGui.InputInt("开拓工坊数量", ref workshops))
            {
                Configuration.numWorkshops = workshops;
                Configuration.Save();
            }
            ImGui.Spacing();
            int currentLevel = (Configuration.workshopBonus - 100) / 10;

            if (ImGui.Combo("开拓工坊等级", ref currentLevel, new string[3] { "等级1", "等级2", "等级3" }, 3))
            {
                Configuration.workshopBonus = currentLevel * 10 + 100;
                Configuration.Save();
            }
            ImGui.Spacing();
            int groove = Configuration.maxGroove;
            if (ImGui.InputInt("总干劲", ref groove))
            {
                Configuration.maxGroove = groove;
                Configuration.Save();
            }
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("高级设置"))
        {
            ImGui.PopItemWidth();
            string flavorText = String.Join(", ", Configuration.flavorText);
            if (ImGui.InputText("关键词隐藏列表", ref flavorText, 1000))
            {
                Configuration.flavorText = flavorText.Split(",");
                for (int i = 0; i < Configuration.flavorText.Length; i++)
                    Configuration.flavorText[i] = Configuration.flavorText[i].Trim();
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("关键词隐藏列表");
            }
            ImGui.PushItemWidth(itemWidth);
            ImGui.Spacing();
            bool enforceRest = Configuration.enforceRestDays;
            if (ImGui.Checkbox("Enforce rest days", ref enforceRest))
            {
                Configuration.enforceRestDays = enforceRest;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("强制在每周的第一个生产日休息");
            }
            if (Configuration.day == 0 && Configuration.unknownD2Items != null && Configuration.unknownD2Items.Count > 0)
            {
                ImGui.Spacing();
                ImGui.Text(Solver.Solver.GetD2PeakDesc());
                ImGui.Spacing();
                var enumerator = Configuration.unknownD2Items.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    bool strong = enumerator.Current.Value;
                    if (ImGui.Checkbox(enumerator.Current.Key + " strong? ", ref strong))
                    {
                        Configuration.unknownD2Items[enumerator.Current.Key] = strong;
                        Configuration.Save();
                    }
                    ImGui.Spacing();
                }
            }
            ImGui.Spacing();
            bool allowOverwrite = Configuration.allowOverwritingDays;
            if (ImGui.Checkbox("允许重写天数", ref allowOverwrite))
            {
                Configuration.allowOverwritingDays = allowOverwrite;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("让你通过输入值将休息日改为手工制作日，这是一个临时功能，直到我得到一个更方便的功能。");
            }
        }
    }
}
