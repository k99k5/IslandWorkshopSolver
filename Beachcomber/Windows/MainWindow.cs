using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Logging;
using Beachcomber.Solver;
using System.Linq;

namespace Beachcomber.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Reader reader;
    private Configuration config;
    private Dictionary<int,SuggestedSchedules?> scheduleSuggestions;
    private List<EndDaySummary> endDaySummaries;
    private int[] selectedSchedules = new int[7];
    private Dictionary<int, int> inventory = new Dictionary<int, int>();
    private Vector4 yellow = new Vector4(1f, 1f, .3f, 1f);
    private Vector4 green = new Vector4(.3f, 1f, .3f, 1f);
    private Vector4 red = new Vector4(1f, .3f, .3f, 1f);
    private bool showInventoryError = false;
    private bool showSupplyError = false;
    private bool showWorkshopError = false;

    private int makeupValue = 0;
    private int makeupGroove = 0;

    public MainWindow(Plugin plugin, Reader reader) : base(
        "Beachcomber", ImGuiWindowFlags.NoScrollbar)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(425, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        this.reader = reader;
        config = plugin.Configuration;
        Solver.Solver.Init(config, this);

        
        scheduleSuggestions = new Dictionary<int, SuggestedSchedules?>();
        endDaySummaries = Solver.Solver.Importer.endDays;
    }

    public override void OnOpen()
    {
        (int day, string data) islandData = reader.ExportIsleData();
        (int maybeRank, int maybeGroove) = reader.GetIslandRankAndMaxGroove();
        bool changedConfig = false;
        if(maybeRank > 0)
        {
            config.islandRank = maybeRank;
            config.maxGroove = maybeGroove;
            changedConfig = true;
        }
        string[] products = islandData.data.Split('\n', StringSplitOptions.None);
        if(reader.GetInventory(out var maybeInv))
            inventory = maybeInv;
        WorkshopInfo? workshopInfo = reader.GetWorkshopInfo();
        if (workshopInfo !=null)
        {
            changedConfig = true;
            showWorkshopError = workshopInfo.ShowError;
            config.workshopBonus = workshopInfo.WorkshopBonus;
            config.numWorkshops = workshopInfo.NumWorkshops;
            PluginLog.Debug("Setting config workshops to {0}", workshopInfo.NumWorkshops);
        }
        else
        {
            PluginLog.Debug("Null workshop info, continuing");
        }
        if (changedConfig)
        {
            config.Save();
            Solver.Solver.Init(config, this); 
        }
        showSupplyError = false;
        try
        {
            if(Solver.Solver.WriteTodaySupply(islandData.day, products))
            {
                Solver.Solver.InitAfterWritingTodaysData();


                base.OnOpen();
            }
            else
            {
                showSupplyError = true;
            }
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Error opening window and writing supply/initing");
            DalamudPlugins.Chat.PrintError("打开窗口错误。/xllog 查看更多信息");
            IsOpen = false;
        }
    }

    public void Dispose()
    {
       
    }

    private string JoinItems(string delimiter, List<Item> items)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < items.Count; i++)
        {
            sb.Append(ItemHelper.GetDisplayName(items[i]));
            if (i < items.Count - 1)
                sb.Append(delimiter);
        }
        return sb.ToString();
    }

    private void AddNewSuggestions(List<(int day, SuggestedSchedules? sch)>? schedules)
    {
        for (int c = 0; c < selectedSchedules.Length; c++)
            selectedSchedules[c] = -1;

        if (schedules != null)
        {
            scheduleSuggestions.Clear();
            foreach (var schedule in schedules)
            {
                scheduleSuggestions.Add(schedule.day, schedule.sch);
            }
        }

        foreach (var schedule in scheduleSuggestions)
        {
            int day = schedule.Key;
            if (Solver.Solver.SchedulesPerDay.ContainsKey(day) && schedule.Value != null)
            {
                int i = 0;
                foreach (var suggestion in schedule.Value.orderedSuggestions)
                {
                    if (suggestion.Key.HasSameCrafts(Solver.Solver.SchedulesPerDay[day].schedule.workshops[0]))
                    {
                        selectedSchedules[day] = i;
                        break;
                    }
                    i++;
                }
            }
        }
    }

    public override void Draw()
    {
        if(showSupplyError)
        {
            ImGui.TextColored(red,
                "不能导入供应信息！ \n" +
                "请在无人岛与监工小员谈话， \n" +
                "打开“工坊生产计划”，然后点击按钮重试。"
            );
            ImGui.Spacing();
            if (ImGui.Button("重试"))
            {
                OnOpen();
            }
            return;
        }
        if(showWorkshopError)
        {
            ImGui.TextColored(yellow, "警告：你有一个准备升级的开拓工房没有确认。请检查所有的管理板");
            ImGui.Spacing();
        }
        try
        {

            endDaySummaries = Solver.Solver.Importer.endDays;
            float buttonWidth = ImGui.GetContentRegionAvail().X / 6;
            if (ImGui.Button("求解", new Vector2(buttonWidth, 0f)))
            {                
                try
                {
                    Solver.Solver.Init(config, this);
                    showInventoryError = false;
                    if (reader.GetInventory(out var maybeInv))
                        inventory = maybeInv;
                    if (config.onlySuggestMaterialsOwned && inventory.Count == 0)
                    {
                        scheduleSuggestions.Clear();
                        showInventoryError = true;
                    }
                    else
                    {
                        List<(int day, SuggestedSchedules? sch)>? schedules = Solver.Solver.RunSolver(inventory);
                        AddNewSuggestions(schedules);
                    }
                    
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Error running solver.");
                }
            }
            ImGui.SameLine(buttonWidth+20);
            string totalCowries = "本周期销售额: " + Solver.Solver.TotalGross;
            if (config.showNetCowries)
                totalCowries += " (" + Solver.Solver.TotalNet + " 净利润)";
            ImGui.Text(totalCowries);

            ImGui.GetContentRegionAvail();
            
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - buttonWidth + 10);
            if (ImGui.Button("设置", new Vector2(buttonWidth,0f)))
            {
                Plugin.DrawConfigUI();
            }
            ImGui.Spacing();

            if(showInventoryError)
            {
                ImGui.TextColored(red, "库存还没有初始化。打开你的开拓包，并点击所有的标签。");
                ImGui.TextColored(red, "或者在设置中关闭 \"必须有稀有材料\"。");
                ImGui.Spacing();
            }

            if (scheduleSuggestions.Count > 1 && config.day > 0)
            {
                ImGui.TextColored(yellow, "有多日的建议可供选择！");
                ImGui.Spacing();
                ImGui.TextWrapped("这些时间表会相互影响! 建议选择价值最高的时间表。");
                ImGui.Spacing();
            
            }

            // Create a new table to show relevant data.
            if ((scheduleSuggestions.Count > 0 || endDaySummaries.Count > 0 || config.day == 6) && ImGui.BeginTabBar("Workshop Schedules"))
            {
                for (int day = 0; day < 7; day++)
                {
                    if (day <= Solver.Solver.CurrentDay && endDaySummaries.Count > day)
                    {
                        if (ImGui.BeginTabItem("生产日 " + (day + 1)))
                        {
                            string title = "Crafted";
                            if (day == Solver.Solver.CurrentDay)
                                title = "Scheduled";
                            if (endDaySummaries[day].crafts.Count > 0 && ImGui.BeginTable(title, 3))
                            {
                                ImGui.TableSetupColumn("产品", ImGuiTableColumnFlags.WidthFixed, 180);
                                ImGui.TableSetupColumn("数量", ImGuiTableColumnFlags.WidthFixed, 100);
                                ImGui.TableSetupColumn("金额", ImGuiTableColumnFlags.WidthFixed, 100);
                                ImGui.TableHeadersRow();


                                for (int i = 0; i < endDaySummaries[day].crafts.Count; i++)
                                {
                                    int column = 0;
                                    ImGui.TableNextRow();
                                    ImGui.TableSetColumnIndex(column++);
                                    ImGui.Text(ItemHelper.GetDisplayName(endDaySummaries[day].crafts[i]));
                                    ImGui.TableSetColumnIndex(column++);
                                    ImGui.Text((i==0?3:6).ToString()); //I'm just hard-coding in that these are efficient, idgaf
                                    ImGui.TableSetColumnIndex(column++);
                                    if(i < endDaySummaries[day].valuesPerCraft.Count)
                                        ImGui.Text(endDaySummaries[day].valuesPerCraft[i].ToString());
                                }
                                ImGui.EndTable();
                                ImGui.Spacing();


                                ImGui.Text("金额: " + endDaySummaries[day].endingGross);
                                ImGui.SameLine(200);
                                ImGui.Text("使用: " + (endDaySummaries[day].endingGross - endDaySummaries[day].endingNet));
                            }
                            else if (endDaySummaries[day].endingGross > 0)
                            {
                                int grooveYesterday = 0;
                                if (day > 0)
                                {
                                    grooveYesterday = endDaySummaries[day - 1].endingGroove;
                                }
                                int grooveToday = endDaySummaries[day].endingGroove - grooveYesterday;

                                ImGui.Text("Made " + endDaySummaries[day].endingGross + " cowries and " + grooveToday + " groove");
                                
                            }
                            else
                            {
                                if(day==Solver.Solver.CurrentDay)
                                    ImGui.Text("休息中");
                                else
                                    ImGui.Text("已休息");

                                if(day > 0 && config.allowOverwritingDays)
                                {
                                    ImGui.Spacing();
                                    ImGui.Text("Is this wrong? Please enter your value and groove for the day");
                                    ImGui.PushItemWidth(200);
                                    ImGui.Spacing();
                                    ImGui.InputInt("金额", ref makeupValue);
                                    ImGui.Spacing();
                                    ImGui.InputInt("产生的干劲", ref makeupGroove);
                                    ImGui.Spacing();
                                    if (ImGui.Button("保存"))
                                    {
                                        PluginLog.Debug("Adding stub value");
                                        Solver.Solver.AddStubValue(day, makeupGroove, makeupValue);
                                        makeupGroove = 0;
                                        makeupValue = 0;
                                    }
                                    ImGui.PopItemWidth();
                                }
                            }
                            ImGui.EndTabItem();
                        }
                    }
                    else if (scheduleSuggestions.ContainsKey(day))
                    {
                        var schedule = scheduleSuggestions[day];
                        if (ImGui.BeginTabItem("生产日 " + (day + 1)))
                        {
                            if (schedule != null)
                            {
                                if(selectedSchedules[day] >= 0)
                                {
                                    var matsRequired = Solver.Solver.GetScheduledMatsNeeded();
                                    if (matsRequired != null)
                                    {
                                        ImGui.Spacing();
                                        if(inventory.Count == 0)
                                        {
                                            ImGui.TextColored(yellow, "打开你的开拓包，并点击所有的标签，确认是否有相应材料");
                                            ImGui.Spacing();
                                            ImGui.TextWrapped(ConvertMatsToString(matsRequired));
                                            if (ImGui.IsItemHovered())
                                            {
                                                ImGui.SetTooltip("带星的物品是稀有材料，来自探索、牧场或耕地。");
                                            }
                                        }
                                        else
                                        {
                                            string matsNeeded = "所需材料：";
                                            float currentX = ImGui.CalcTextSize(matsNeeded).X;
                                            float availableX = ImGui.GetContentRegionAvail().X;
                                            ImGui.Text(matsNeeded);
                                            foreach (var mat in matsRequired)
                                            {

                                                bool isRare = RareMaterialHelper.GetMaterialValue(mat.Key, out _);
                                                string matStr = mat.Value + "x " + RareMaterialHelper.GetDisplayName(mat.Key) + (isRare ? "*" : ""); 
                                                if (!mat.Equals(matsRequired.Last()))
                                                    matStr += ", ";
                                                Vector4 color = red;
                                                string tooltip = "";
                                                if(inventory.ContainsKey((int)mat.Key))
                                                {
                                                    int itemsHeld = inventory[(int)mat.Key];
                                                    if (itemsHeld >= mat.Value)
                                                        color = green;
                                                    else if (itemsHeld*2 >= mat.Value)
                                                        color = yellow;
                                                    tooltip = "Owned: " + itemsHeld + ". ";
                                                }
                                                currentX += ImGui.CalcTextSize(matStr).X;
                                                if (currentX < availableX)
                                                    ImGui.SameLine(0f, 0f);
                                                else
                                                    currentX = ImGui.CalcTextSize(matStr).X;

                                                ImGui.TextColored(color, matStr);
                                                if (isRare)
                                                    tooltip += "带星的物品是稀有材料，来自探索、牧场或耕地。";
                                                if (ImGui.IsItemHovered())
                                                {
                                                    ImGui.SetTooltip(tooltip);
                                                }
                                            }
                                        }                                        
                                        ImGui.Spacing();
                                    }
                                }
                                if (ImGui.BeginTable("Options", 4, ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
                                {
                                    /*ImGui.TableSetupColumn("Confirmed", ImGuiTableColumnFlags.WidthStretch);*/
                                    ImGui.TableSetupColumn("使用？", ImGuiTableColumnFlags.WidthFixed, 50);
                                    ImGui.TableSetupColumn("权重价值", ImGuiTableColumnFlags.WidthFixed, 100);
                                    ImGui.TableSetupColumn("生产物品", ImGuiTableColumnFlags.WidthStretch, 250);
                                    ImGui.TableHeadersRow();

                                    var enumerator = schedule.orderedSuggestions.GetEnumerator();

                                    for (int i = 0; i < config.suggestionsToShow && enumerator.MoveNext(); i++)
                                    {
                                        var suggestion = enumerator.Current;
                                        int column = 0;
                                        ImGui.TableNextRow();
                                        /*ImGui.TableSetColumnIndex(column++);
                                        ImGui.Text((!suggestion.Key.hasAnyUnsurePeaks()).ToString());*/
                                        ImGui.TableSetColumnIndex(column++);
                                        if (ImGui.RadioButton("##" + (i + 1), ref selectedSchedules[day], i))
                                        {
                                            if (reader.GetInventory(out var maybeInv))
                                                inventory = maybeInv;

                                            Solver.Solver.SetDay(suggestion.Key.GetItems(), day);
                                            AddNewSuggestions(Solver.Solver.RunSolver(inventory));
                                        }
                                        ImGui.TableSetColumnIndex(column++);
                                        ImGui.Text(suggestion.Value.ToString());
                                        ImGui.TableSetColumnIndex(column++);
                                        if (suggestion.Key.GetNumCrafts() > 0)
                                            ImGui.Text(JoinItems(" - ", suggestion.Key.GetItems()));
                                        else
                                            ImGui.Text("Rest");
                                    }
                                    ImGui.EndTable();
                                }
                                ImGui.Spacing();
                            }
                            else
                            {
                                ImGui.Text("Rest!");
                            }
                            ImGui.EndTabItem();
                        }
                    }
                }

                if(config.day == 6)
                {
                    if (ImGui.BeginTabItem("Day 1 Next Week"))
                    {
                        ImGui.Text("第一天总是休息");
                    }
                }

                ImGui.EndTabBar();
            }
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Error displaying schedule data.");
        }
    }

    private string ConvertMatsToString(IOrderedEnumerable<KeyValuePair<Material, int>> orderedDict)
    {
        var matsEnum = orderedDict.GetEnumerator();
        StringBuilder matsStr = new StringBuilder("所需材料：");
        while (matsEnum.MoveNext())
        {
            matsStr.Append(matsEnum.Current.Value).Append("x ").Append(RareMaterialHelper.GetDisplayName(matsEnum.Current.Key));
            if (RareMaterialHelper.GetMaterialValue(matsEnum.Current.Key, out _))
                matsStr.Append('*');
            if (!matsEnum.Current.Equals(orderedDict.Last()))
                matsStr.Append(", ");
        }
        return matsStr.ToString();
    }
}
