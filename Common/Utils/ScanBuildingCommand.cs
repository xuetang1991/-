// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-24

using System;
using System.IO;
using System.Text;

using Microsoft.Xna.Framework;

using Terraria;
using Terraria.ModLoader;

namespace sea.Common.Utils;

/// <summary>
/// 扫描指令：/scanbuilding X Y 宽度 高度
/// 例如：/scanbuilding 100 50 60 40
/// </summary>
public class ScanBuildingCommand : ModCommand
{
    public override CommandType Type => CommandType.Chat;
    public override string Command => "scanbuilding";
    public override string Usage => "/scanbuilding <左上角X> <左上角Y> <宽度> <高度>";
    public override string Description => "扫描建筑并生成代码，保存到桌面";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length < 4)
        {
            caller.Reply("用法: /scanbuilding 100 50 60 40", Color.Red);
            caller.Reply("参数：左上角X坐标 左上角Y坐标 宽度 高度", Color.Yellow);
            return;
        }

        int x = int.Parse(args[0]);
        int y = int.Parse(args[1]);
        int w = int.Parse(args[2]);
        int h = int.Parse(args[3]);

        caller.Reply($"开始扫描区域: ({x},{y}) 大小 {w}x{h}...", Color.Cyan);

        // 扫描并生成代码
        string code = ScanAndGenerateCode(x, y, w, h);

        // 保存到桌面
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string fileName = $"Building_{w}x{h}_{DateTime.Now:MMdd_HHmmss}.cs";
        string fullPath = Path.Combine(desktopPath, fileName);

        File.WriteAllText(fullPath, code, Encoding.UTF8);

        caller.Reply($"✓ 扫描完成！代码已保存到桌面:", Color.Green);
        caller.Reply($"  {fileName}", Color.Lime);
        caller.Reply("复制文件内容到 FarSea.cs 即可使用", Color.Yellow);
    }

    private string ScanAndGenerateCode(int startX, int startY, int width, int height)
    {
        StringBuilder sb = new();

        // 生成注释
        sb.AppendLine("// ========================================");
        sb.AppendLine($"// 建筑扫描结果 - {width}x{height}");
        sb.AppendLine($"// 原始位置: ({startX}, {startY})");
        sb.AppendLine($"// 扫描时间: {DateTime.Now}");
        sb.AppendLine("// ========================================");
        sb.AppendLine();

        // 1. 扫描方块数据
        sb.AppendLine("private static ushort[,] tileData = new ushort[,]");
        sb.AppendLine("{");
        for (int dy = 0; dy < height; dy++)
        {
            sb.Append("    { ");
            for (int dx = 0; dx < width; dx++)
            {
                Tile tile = Main.tile[startX + dx, startY + dy];
                ushort tileType = tile.HasTile ? tile.TileType : (ushort)0;
                sb.Append(tileType.ToString().PadLeft(3));
                if (dx < width - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            if (dy < height - 1)
            {
                sb.Append(",");
            }

            sb.AppendLine();
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // 2. 扫描墙壁数据
        sb.AppendLine("private static ushort[,] wallData = new ushort[,]");
        sb.AppendLine("{");
        for (int dy = 0; dy < height; dy++)
        {
            sb.Append("    { ");
            for (int dx = 0; dx < width; dx++)
            {
                Tile tile = Main.tile[startX + dx, startY + dy];
                ushort wallType = tile.WallType;
                sb.Append(wallType.ToString().PadLeft(3));
                if (dx < width - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            if (dy < height - 1)
            {
                sb.Append(",");
            }

            sb.AppendLine();
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // 3. 扫描方块形状（斜坡/半砖）
        sb.AppendLine("private static byte[,] tileSlope = new byte[,]");
        sb.AppendLine("{");
        for (int dy = 0; dy < height; dy++)
        {
            sb.Append("    { ");
            for (int dx = 0; dx < width; dx++)
            {
                Tile tile = Main.tile[startX + dx, startY + dy];
                byte slope = (byte)(tile.HasTile ? (int)tile.Slope : 0);
                sb.Append(slope.ToString());
                if (dx < width - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            if (dy < height - 1)
            {
                sb.Append(",");
            }

            sb.AppendLine();
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // 4. 扫描液体类型
        sb.AppendLine("private static byte[,] liquidType = new byte[,]");
        sb.AppendLine("{");
        for (int dy = 0; dy < height; dy++)
        {
            sb.Append("    { ");
            for (int dx = 0; dx < width; dx++)
            {
                Tile tile = Main.tile[startX + dx, startY + dy];
                byte type = tile.LiquidAmount > 0 ? (byte)tile.LiquidType : (byte)0;
                sb.Append(type.ToString());
                if (dx < width - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            if (dy < height - 1)
            {
                sb.Append(",");
            }

            sb.AppendLine();
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // 5. 扫描液体数量
        sb.AppendLine("private static byte[,] liquidAmount = new byte[,]");
        sb.AppendLine("{");
        for (int dy = 0; dy < height; dy++)
        {
            sb.Append("    { ");
            for (int dx = 0; dx < width; dx++)
            {
                Tile tile = Main.tile[startX + dx, startY + dy];
                sb.Append(tile.LiquidAmount.ToString().PadLeft(3));
                if (dx < width - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            if (dy < height - 1)
            {
                sb.Append(",");
            }

            sb.AppendLine();
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // 6. 扫描红线
        sb.AppendLine("private static bool[,] wireRed = new bool[,]");
        sb.AppendLine("{");
        for (int dy = 0; dy < height; dy++)
        {
            sb.Append("    { ");
            for (int dx = 0; dx < width; dx++)
            {
                Tile tile = Main.tile[startX + dx, startY + dy];
                sb.Append(tile.RedWire ? "true " : "false");
                if (dx < width - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            if (dy < height - 1)
            {
                sb.Append(",");
            }

            sb.AppendLine();
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // 7. 扫描蓝线
        sb.AppendLine("private static bool[,] wireBlue = new bool[,]");
        sb.AppendLine("{");
        for (int dy = 0; dy < height; dy++)
        {
            sb.Append("    { ");
            for (int dx = 0; dx < width; dx++)
            {
                Tile tile = Main.tile[startX + dx, startY + dy];
                sb.Append(tile.BlueWire ? "true " : "false");
                if (dx < width - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            if (dy < height - 1)
            {
                sb.Append(",");
            }

            sb.AppendLine();
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // 8. 扫描绿线
        sb.AppendLine("private static bool[,] wireGreen = new bool[,]");
        sb.AppendLine("{");
        for (int dy = 0; dy < height; dy++)
        {
            sb.Append("    { ");
            for (int dx = 0; dx < width; dx++)
            {
                Tile tile = Main.tile[startX + dx, startY + dy];
                sb.Append(tile.GreenWire ? "true " : "false");
                if (dx < width - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            if (dy < height - 1)
            {
                sb.Append(",");
            }

            sb.AppendLine();
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // 9. 扫描黄线
        sb.AppendLine("private static bool[,] wireYellow = new bool[,]");
        sb.AppendLine("{");
        for (int dy = 0; dy < height; dy++)
        {
            sb.Append("    { ");
            for (int dx = 0; dx < width; dx++)
            {
                Tile tile = Main.tile[startX + dx, startY + dy];
                sb.Append(tile.YellowWire ? "true " : "false");
                if (dx < width - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            if (dy < height - 1)
            {
                sb.Append(",");
            }

            sb.AppendLine();
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // 10. 扫描机关触发状态（Actuator + Inactivity）
        sb.AppendLine("private static bool[,] tileActuator = new bool[,]");
        sb.AppendLine("{");
        for (int dy = 0; dy < height; dy++)
        {
            sb.Append("    { ");
            for (int dx = 0; dx < width; dx++)
            {
                Tile tile = Main.tile[startX + dx, startY + dy];
                sb.Append(tile.HasActuator ? "true " : "false");
                if (dx < width - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            if (dy < height - 1)
            {
                sb.Append(",");
            }

            sb.AppendLine();
        }
        sb.AppendLine("};");
        sb.AppendLine();

        sb.AppendLine("private static bool[,] tileInactive = new bool[,]");
        sb.AppendLine("{");
        for (int dy = 0; dy < height; dy++)
        {
            sb.Append("    { ");
            for (int dx = 0; dx < width; dx++)
            {
                Tile tile = Main.tile[startX + dx, startY + dy];
                sb.Append(tile.IsActuated ? "true " : "false");
                if (dx < width - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            if (dy < height - 1)
            {
                sb.Append(",");
            }

            sb.AppendLine();
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // 生成放置函数
        sb.AppendLine("// 调用方法：PlaceScannedBuilding(originX, originY);");
        sb.AppendLine("private void PlaceScannedBuilding(int originX, int originY)");
        sb.AppendLine("{");
        sb.AppendLine($"    int width = {width};");
        sb.AppendLine($"    int height = {height};");
        sb.AppendLine();
        sb.AppendLine("    for (int dy = 0; dy < height; dy++)");
        sb.AppendLine("    {");
        sb.AppendLine("        for (int dx = 0; dx < width; dx++)");
        sb.AppendLine("        {");
        sb.AppendLine("            int x = originX + dx;");
        sb.AppendLine("            int y = originY + dy;");
        sb.AppendLine("            Tile tile = Main.tile[x, y];");
        sb.AppendLine();
        sb.AppendLine("            // 放置方块");
        sb.AppendLine("            ushort tileType = tileData[dy, dx];");
        sb.AppendLine("            if (tileType == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                tile.HasTile = false;");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                tile.HasTile = true;");
        sb.AppendLine("                tile.TileType = tileType;");
        sb.AppendLine("                ");
        sb.AppendLine("                // 设置方块形状（斜坡/半砖）");
        sb.AppendLine("                tile.Slope = (SlopeType)tileSlope[dy, dx];");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // 放置墙壁");
        sb.AppendLine("            tile.WallType = wallData[dy, dx];");
        sb.AppendLine();
        sb.AppendLine("            // 放置液体");
        sb.AppendLine("            if (liquidAmount[dy, dx] > 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                tile.LiquidAmount = liquidAmount[dy, dx];");
        sb.AppendLine("                tile.LiquidType = liquidType[dy, dx];");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // 放置电路");
        sb.AppendLine("            tile.RedWire = wireRed[dy, dx];");
        sb.AppendLine("            tile.BlueWire = wireBlue[dy, dx];");
        sb.AppendLine("            tile.GreenWire = wireGreen[dy, dx];");
        sb.AppendLine("            tile.YellowWire = wireYellow[dy, dx];");
        sb.AppendLine();
        sb.AppendLine("            // 放置驱动器状态");
        sb.AppendLine("            tile.HasActuator = tileActuator[dy, dx];");
        sb.AppendLine("            tile.IsActuated = tileInactive[dy, dx];");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // 刷新边框");
        sb.AppendLine("    for (int dy = 0; dy < height; dy++)");
        sb.AppendLine("        for (int dx = 0; dx < width; dx++)");
        sb.AppendLine("        {");
        sb.AppendLine("            WorldGen.SquareTileFrame(originX + dx, originY + dy, true);");
        sb.AppendLine("            if (Main.netMode == NetmodeID.Server)");
        sb.AppendLine("                NetMessage.SendTileSquare(-1, originX + dx, originY + dy, 1);");
        sb.AppendLine("        }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
