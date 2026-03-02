using System.Text;

namespace Infrastructure.Services;

internal static class EmailBallRenderer
{
    public static string RenderBall(int value, bool bonus = false, int size = 30)
    {
        var background = bonus
            ? "#bfdbfe"
            : "#fed7aa";
        var border = bonus
            ? "#60a5fa"
            : "#fb923c";
        var fontSize = size >= 40 ? 15 : 13;

        return
            $"<span style=\"display:inline-block;width:{size}px;height:{size}px;line-height:{size}px;text-align:center;vertical-align:middle;border-radius:50%;margin:0 6px 6px 0;background-color:{background};color:#0f172a;font-size:{fontSize}px;font-weight:800;font-family:Arial,sans-serif;border:1px solid {border};box-sizing:border-box;mso-line-height-rule:exactly;\">{value}</span>";
    }

    public static string RenderBallRow(IEnumerable<int> numbers, bool bonus = false, int size = 30)
    {
        var builder = new StringBuilder();
        builder.Append("<span style=\"display:inline-block;vertical-align:middle;line-height:0;\">");

        foreach (var number in numbers)
        {
            builder.Append(RenderBall(number, bonus, size));
        }

        builder.Append("</span>");
        return builder.ToString();
    }
}
