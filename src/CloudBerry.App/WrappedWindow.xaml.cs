using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App;

public partial class WrappedWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly bool _allTime;

    public WrappedWindow(MainViewModel vm, bool allTime = false)
    {
        _vm = vm;
        _allTime = allTime;
        InitializeComponent();
        DataContext = _vm;
        _ = WrappedBrowser.EnsureCoreWebView2Async();
    }

    private void TitleBarDrag(object sender, MouseButtonEventArgs e) => DragMove();
    private void CloseClick(object sender, RoutedEventArgs e)        => Close();

    private void OnBrowserReady(object sender,
        Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;
        WrappedBrowser.CoreWebView2.NavigateToString(BuildHtml());
    }

    private string BuildHtml()
    {
        var year    = _allTime ? 0 : DateTime.Now.Year;
        var wrapped = _vm.GetWrappedInsights(year);

        // Build 53-week x 7-day heatmap from session data (single-year view only —
        // an all-time view spans multiple years so the day-of-week/time-of-day
        // charts below are the "Play Patterns" replacement instead).
        var heatRows = Array.Empty<int[]>();
        if (!_allTime)
        {
            var heatmap = new int[53, 7];
            foreach (var game in _vm.Games)
            {
                foreach (var s in game.Sessions.Where(s => s.StartedAtLocal.Year == year))
                {
                    int dayOfYear = s.StartedAtLocal.DayOfYear - 1;
                    int week      = Math.Min(dayOfYear / 7, 52);
                    int dow       = (int)s.StartedAtLocal.DayOfWeek;
                    heatmap[week, dow] += (int)s.DurationMinutes;
                }
            }

            heatRows = new int[53][];
            for (int w = 0; w < 53; w++)
            {
                heatRows[w] = new int[7];
                for (int d = 0; d < 7; d++)
                    heatRows[w][d] = heatmap[w, d];
            }
        }

        // Use the actual WrappedInsights property names from MainViewModel
        var jsonData = JsonSerializer.Serialize(new
        {
            year,
            allTime        = _allTime,
            hasData        = wrapped.HasData,
            totalMinutes   = wrapped.TotalMinutes,
            totalHours     = wrapped.TotalHours,
            sessions       = wrapped.SessionCount,
            topGame        = wrapped.TopGameTitle,
            topGameMinutes = wrapped.TopGameMinutes,
            peakMonth      = wrapped.PeakMonthName,
            peakDay        = wrapped.PeakDayName,
            peakHour       = wrapped.PeakHourLabel,
            newGames       = wrapped.UniqueGames,
            topGenre       = wrapped.FavoriteGenre,
            achievements   = wrapped.AchievementsUnlocked,
            heatmap        = heatRows,
            timeOfDay      = wrapped.TimeOfDayMinutes,
            dayOfWeek      = wrapped.DayOfWeekMinutes,
            sessionLengths = wrapped.SessionLengthCounts,
        });

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><style>");
        sb.Append("*{margin:0;padding:0;box-sizing:border-box}");
        sb.Append("html,body{width:100%;height:100%;background:#0D0D10;color:#EAEAEA;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;overflow:hidden}");
        sb.Append(".card{display:flex;flex-direction:column;gap:20px;padding:28px 32px;height:100%;overflow-y:auto}");
        sb.Append(".card::-webkit-scrollbar{width:4px}.card::-webkit-scrollbar-thumb{background:#333;border-radius:2px}");
        sb.Append("h1{font-size:28px;font-weight:800;background:linear-gradient(135deg,#E86A33,#C850C0,#4158D0);-webkit-background-clip:text;-webkit-text-fill-color:transparent;letter-spacing:-0.5px}");
        sb.Append(".sub{font-size:13px;color:#666;margin-top:4px}");
        sb.Append(".stats{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}");
        sb.Append(".stat{background:#18181C;border:1px solid #222;border-radius:12px;padding:16px;display:flex;flex-direction:column;gap:4px}");
        sb.Append(".stat-val{font-size:26px;font-weight:700;color:#fff}.stat-lbl{font-size:11px;color:#666;text-transform:uppercase;letter-spacing:.5px}");
        sb.Append(".hl{background:linear-gradient(135deg,#1a1a2e,#16213e);border:1px solid #2a2a4a;border-radius:12px;padding:18px 20px}");
        sb.Append(".hl-title{font-size:11px;color:#7878AA;text-transform:uppercase;letter-spacing:.8px;margin-bottom:8px}");
        sb.Append(".hl-val{font-size:18px;font-weight:600;color:#fff}");
        sb.Append(".hm-wrap{background:#18181C;border:1px solid #222;border-radius:12px;padding:16px}");
        sb.Append(".hm-title{font-size:11px;color:#666;text-transform:uppercase;letter-spacing:.5px;margin-bottom:12px}");
        sb.Append(".heatmap{display:flex;gap:3px}.week{display:flex;flex-direction:column;gap:3px}");
        sb.Append(".cell{width:10px;height:10px;border-radius:2px}");
        sb.Append(".no-data{display:flex;align-items:center;justify-content:center;height:300px;flex-direction:column;gap:12px;color:#444}");
        sb.Append(".bars{display:flex;align-items:flex-end;gap:3px;height:90px}");
        sb.Append(".bar{flex:1;background:linear-gradient(180deg,#C850C0,#4158D0);border-radius:2px 2px 0 0;min-height:2px}");
        sb.Append(".bar-lbls{display:flex;gap:3px;margin-top:6px}.bar-lbl{flex:1;text-align:center;font-size:9px;color:#555}");
        sb.Append(".pattern-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}");
        sb.Append("</style></head><body><div class='card' id='root'></div><script>");
        sb.Append("const d=");
        sb.Append(jsonData);
        sb.Append(";const root=document.getElementById('root');");

        // No data state
        sb.Append("if(!d.hasData){const lbl=d.allTime?'yet':('for '+d.year);root.innerHTML=\"<div class='no-data'><div style='font-size:48px'>&#x1F3AE;</div><div style='font-size:15px;font-weight:600'>No playtime recorded \"+lbl+\"</div><div style='font-size:13px;color:#555'>Play some games to build your \"+(d.allTime?'Play Patterns':'Year Wrapped')+\".</div></div>\";}");

        // Main content
        sb.Append("else{");
        sb.Append("const fmt=m=>m<60?m+'m':Math.floor(m/60)+'h '+(m%60?m%60+'m':'');");
        sb.Append("let html=d.allTime");
        sb.Append("?\"<div><h1>All-Time Play Patterns</h1><div class='sub'>How you actually play, across every year</div></div>\"");
        sb.Append(":\"<div><h1>\"+d.year+\" Year Wrapped</h1><div class='sub'>Your gaming year at a glance</div></div>\";");

        // Stats row
        sb.Append("html+=\"<div class='stats'>\";");
        sb.Append("html+=\"<div class='stat'><div class='stat-val'>\"+d.totalHours+\"h</div><div class='stat-lbl'>Total playtime</div></div>\";");
        sb.Append("html+=\"<div class='stat'><div class='stat-val'>\"+d.sessions+\"</div><div class='stat-lbl'>Sessions</div></div>\";");
        sb.Append("html+=\"<div class='stat'><div class='stat-val'>\"+d.newGames+\"</div><div class='stat-lbl'>Games played</div></div></div>\";");

        // Highlights
        sb.Append("html+=\"<div class='stats'>\";");
        sb.Append("if(d.topGame)html+=\"<div class='hl' style='grid-column:span 2'><div class='hl-title'>Most played game</div><div class='hl-val'>\"+d.topGame+\"</div><div style='font-size:12px;color:#555;margin-top:4px'>\"+fmt(d.topGameMinutes)+\"</div></div>\";");
        sb.Append("if(d.topGenre)html+=\"<div class='hl'><div class='hl-title'>Top genre</div><div class='hl-val'>\"+d.topGenre+\"</div></div>\";");
        sb.Append("html+=\"</div>\";");

        sb.Append("html+=\"<div class='stats'>\";");
        sb.Append("if(d.peakMonth&&!d.allTime)html+=\"<div class='hl'><div class='hl-title'>Peak month</div><div class='hl-val'>\"+d.peakMonth+\"</div></div>\";");
        sb.Append("if(d.peakDay)html+=\"<div class='hl'><div class='hl-title'>Favourite day</div><div class='hl-val'>\"+d.peakDay+\"</div></div>\";");
        sb.Append("if(d.peakHour)html+=\"<div class='hl'><div class='hl-title'>Peak hour</div><div class='hl-val'>\"+d.peakHour+\"</div></div>\";");
        sb.Append("if(d.achievements)html+=\"<div class='hl'><div class='hl-title'>Achievements</div><div class='hl-val'>\"+d.achievements+\"</div></div>\";");
        sb.Append("html+=\"</div>\";");

        // Heatmap (single-year view only)
        sb.Append("if(!d.allTime){");
        sb.Append("const max=Math.max(1,...d.heatmap.flat());");
        sb.Append("const colour=v=>{const a=v/max;if(a<0.05)return'#1a1a1a';if(a<0.25)return'#2d1b4e';if(a<0.5)return'#6b2fa0';if(a<0.75)return'#b44fd9';return'#e879f9';};");
        sb.Append("let cells='';for(let w=0;w<d.heatmap.length;w++){cells+=\"<div class='week'>\";for(let day=0;day<7;day++){const v=d.heatmap[w][day];cells+=\"<div class='cell' style='background:\"+colour(v)+\"' title='\"+(v?fmt(v):'')+\"'></div>\";}cells+=\"</div>\";}");
        sb.Append("html+=\"<div class='hm-wrap'><div class='hm-title'>Daily activity &#x2014; \"+d.year+\"</div><div class='heatmap'>\"+cells+\"</div></div>\";");
        sb.Append("}");

        // Session Replay Timeline — time-of-day + day-of-week bars, session length breakdown
        sb.Append("const bar=(vals,lbls)=>{const m=Math.max(1,...vals);const b=vals.map(v=>\"<div class='bar' style='height:\"+Math.max(2,Math.round(v/m*90))+\"px' title='\"+fmt(v)+\"'></div>\").join('');const l=lbls.map(x=>\"<div class='bar-lbl'>\"+x+\"</div>\").join('');return \"<div class='bars'>\"+b+\"</div><div class='bar-lbls'>\"+l+\"</div>\";};");
        sb.Append("const hourLbls=['12a','','','','','','6a','','','','','','12p','','','','','','6p','','','','',''];");
        sb.Append("const dowLbls=['S','M','T','W','T','F','S'];");
        sb.Append("html+=\"<div class='pattern-grid'>\";");
        sb.Append("html+=\"<div class='hm-wrap'><div class='hm-title'>Time of day</div>\"+bar(d.timeOfDay,hourLbls)+\"</div>\";");
        sb.Append("html+=\"<div class='hm-wrap'><div class='hm-title'>Day of week</div>\"+bar(d.dayOfWeek,dowLbls)+\"</div>\";");
        sb.Append("html+=\"</div>\";");
        sb.Append("const lenLbls=['<15m','15-30m','30-60m','1-2h','2h+'];");
        sb.Append("html+=\"<div class='hm-wrap'><div class='hm-title'>Session length</div>\"+bar(d.sessionLengths,lenLbls)+\"</div>\";");

        sb.Append("root.innerHTML=html;}");
        sb.Append("</script></body></html>");
        return sb.ToString();
    }
}
