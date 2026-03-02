using System.Text;

namespace Infrastructure.Services;

internal static class EmailBallRenderer
{
    public static string RenderBall(int value, bool bonus = false, int size = 30)
    {
        var background = bonus
            ? "linear-gradient(140deg,#60a5fa,#2563eb)"
            : "linear-gradient(140deg,#fb923c,#ea580c)";
        var border = bonus
            ? "rgba(37,99,235,0.45)"
            : "rgba(234,88,12,0.45)";
        var fontSize = size >= 40 ? 15 : 13;

        return
            $"<span style=\"display:inline-flex;align-items:center;justify-content:center;width:{size}px;height:{size}px;border-radius:999px;margin:0 6px 6px 0;background:{background};color:#ffffff;font-size:{fontSize}px;font-weight:800;line-height:1;box-shadow:inset 0 -5px 8px rgba(0,0,0,0.18),0 3px 8px rgba(15,23,42,0.22);border:1px solid {border};\">{value}</span>";
    }

    public static string RenderBallRow(IEnumerable<int> numbers, bool bonus = false, int size = 30)
    {
        var builder = new StringBuilder();
        builder.Append("<span style=\"display:inline-flex;flex-wrap:wrap;vertical-align:middle;\">");

        foreach (var number in numbers)
        {
            builder.Append(RenderBall(number, bonus, size));
        }

        builder.Append("</span>");
        return builder.ToString();
    }
}
