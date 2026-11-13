using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>
/// Pictures of the weapon bar (O8:131C–1EE9, BOMB.OVR 0x1D3DC): per field a fixed list of PutPixel, Line, Bar, Circle and
/// OutTextXY calls relative to the field's viewport, all in the field colour.
/// </summary>
/// <remarks>
/// [ORIGINAL] Coordinates as the calls in O8:1268 pass them (extracted from the disassembly); 18 and 19 are the
/// letters "H" (O8:1264) and "V" (O8:1266) for Herb and Vick, centred at (7, 6) in the DefaultFont. Checked as part of
/// the screen at messpunkt K (PlayfieldScreenOriginalTests).
/// </remarks>
internal static class WeaponPictures
{
    public static void Draw(int field, PlayfieldScreen.Cell cell, GraphText text)
    {
        switch (field)
        {
            case 0:
                cell.PutPixel(7, 5);
                break;
            case 1:
                cell.CircleRadius1(7, 5);
                break;
            case 2:
                cell.PutPixel(7, 5);
                cell.CircleRadius1(7, 5);
                break;
            case 3:
                cell.Line(5, 4, 5, 6);
                cell.Line(5, 5, 9, 5);
                cell.Bar(7, 4, 8, 6);
                break;
            case 4:
                cell.Line(4, 4, 4, 6);
                cell.Line(4, 5, 10, 5);
                cell.Bar(6, 4, 9, 6);
                cell.Bar(7, 3, 8, 7);
                break;
            case 5:
                cell.Line(2, 9, 5, 9);
                cell.Line(3, 8, 4, 8);
                cell.PutPixel(5, 7);
                cell.Line(7, 5, 10, 2);
                break;
            case 6:
                cell.PutPixel(7, 4);
                cell.PutPixel(9, 5);
                cell.PutPixel(5, 5);
                cell.PutPixel(7, 6);
                break;
            case 7:
                cell.PutPixel(7, 5);
                cell.PutPixel(8, 4);
                cell.PutPixel(9, 3);
                cell.PutPixel(10, 3);
                cell.PutPixel(11, 4);
                cell.PutPixel(6, 4);
                cell.PutPixel(5, 3);
                cell.PutPixel(4, 3);
                cell.PutPixel(3, 4);
                cell.PutPixel(6, 5);
                cell.PutPixel(5, 6);
                cell.PutPixel(4, 7);
                cell.PutPixel(4, 8);
                cell.PutPixel(8, 5);
                cell.PutPixel(9, 6);
                cell.PutPixel(10, 7);
                cell.PutPixel(10, 8);
                cell.PutPixel(7, 6);
                cell.PutPixel(7, 7);
                cell.PutPixel(7, 8);
                cell.PutPixel(7, 9);
                break;
            case 8:
                cell.PutPixel(7, 4);
                cell.PutPixel(7, 2);
                cell.PutPixel(7, 6);
                cell.PutPixel(7, 8);
                cell.PutPixel(5, 3);
                cell.PutPixel(9, 3);
                cell.PutPixel(5, 7);
                cell.PutPixel(9, 7);
                break;
            case 9:
                cell.PutPixel(3, 2);
                cell.PutPixel(5, 2);
                cell.PutPixel(7, 2);
                cell.PutPixel(9, 2);
                cell.PutPixel(11, 2);
                cell.PutPixel(3, 4);
                cell.PutPixel(5, 4);
                cell.PutPixel(7, 4);
                cell.PutPixel(9, 4);
                cell.PutPixel(11, 4);
                cell.PutPixel(3, 6);
                cell.PutPixel(5, 6);
                cell.PutPixel(7, 6);
                cell.PutPixel(9, 6);
                cell.PutPixel(11, 6);
                cell.PutPixel(3, 8);
                cell.PutPixel(5, 8);
                cell.PutPixel(7, 8);
                cell.PutPixel(9, 8);
                cell.PutPixel(11, 8);
                break;
            case 10:
                cell.PutPixel(3, 2);
                cell.PutPixel(5, 2);
                cell.PutPixel(7, 2);
                cell.PutPixel(9, 2);
                cell.PutPixel(11, 2);
                cell.PutPixel(4, 4);
                cell.PutPixel(6, 4);
                cell.PutPixel(8, 4);
                cell.PutPixel(10, 4);
                cell.PutPixel(12, 4);
                cell.PutPixel(3, 6);
                cell.PutPixel(5, 6);
                cell.PutPixel(7, 6);
                cell.PutPixel(9, 6);
                cell.PutPixel(11, 6);
                cell.PutPixel(4, 8);
                cell.PutPixel(6, 8);
                cell.PutPixel(8, 8);
                cell.PutPixel(10, 8);
                cell.PutPixel(12, 8);
                break;
            case 11:
                cell.Bar(6, 5, 8, 7);
                cell.Line(5, 6, 9, 6);
                cell.Line(7, 6, 7, 3);
                cell.Line(8, 2, 9, 2);
                break;
            case 12:
                cell.Line(6, 3, 8, 5);
                cell.Line(9, 3, 7, 5);
                cell.Line(6, 7, 8, 5);
                cell.Line(9, 7, 7, 5);
                cell.Line(6, 5, 9, 5);
                break;
            case 13:
                cell.Line(5, 4, 5, 6);
                cell.Line(9, 4, 9, 6);
                cell.Line(6, 3, 8, 3);
                cell.Line(6, 7, 8, 7);
                break;
            case 14:
                cell.Line(5, 4, 5, 6);
                cell.Line(9, 4, 9, 6);
                cell.Bar(6, 3, 8, 7);
                break;
            case 15:
                cell.Bar(6, 4, 8, 6);
                cell.Line(4, 5, 1, 5);
                cell.Line(4, 5, 2, 3);
                cell.Line(4, 5, 2, 7);
                cell.Line(10, 5, 13, 5);
                cell.Line(10, 5, 12, 3);
                cell.Line(10, 5, 12, 7);
                break;
            case 16:
                cell.CircleRadius1(7, 5);
                cell.PutPixel(7, 5);
                cell.Line(4, 5, 1, 5);
                cell.Line(4, 5, 2, 3);
                cell.Line(4, 5, 2, 7);
                cell.Line(10, 5, 13, 5);
                cell.Line(10, 5, 12, 3);
                cell.Line(10, 5, 12, 7);
                cell.Line(7, 2, 7, -1);
                cell.Line(7, 2, 5, 0);
                cell.Line(7, 2, 9, 0);
                cell.Line(7, 8, 7, 11);
                cell.Line(7, 8, 5, 10);
                cell.Line(7, 8, 9, 10);
                break;
            case 17:
                cell.CircleRadius1(7, 7);
                cell.PutPixel(7, 7);
                cell.Line(4, 7, 1, 7);
                cell.Line(1, 7, 3, 5);
                cell.Line(1, 7, 3, 9);
                cell.Line(10, 7, 13, 7);
                cell.Line(13, 7, 11, 5);
                cell.Line(13, 7, 11, 9);
                cell.Line(7, 4, 7, 1);
                cell.Line(7, 1, 5, 3);
                cell.Line(7, 1, 9, 3);
                break;
            case 18:
                text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
                cell.Text(text, 7, 6, "H");
                break;
            case 19:
                text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
                cell.Text(text, 7, 6, "V");
                break;
        }
    }
}
