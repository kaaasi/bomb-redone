using System.Text;
using BombRedone.Core.Setup;
using BombRedone.Tests.Support;

namespace BombRedone.Core.Tests.Setup;

/// <summary>Names of the computer opponents against BOMB.EXE (DS:007C + 256 · k).</summary>
public class ComputerOpponentsOriginalTests
{
    [GameDataFact]
    public void NamesMatchThePascalStringsInBombExe()
    {
        var exe = File.ReadAllBytes(TestGameData.Load().Find("BOMB.EXE")!);
        for (var k = 0; k < ComputerOpponents.Names.Count; k++)
        {
            var at = ComputerOpponents.FileOffset(k);
            var name = Encoding.Latin1.GetString(exe, at + 1, exe[at]);
            Assert.Equal(ComputerOpponents.Names[k], name);
        }
    }

    [GameDataFact]
    public void DocumentationListsTheSevenIntelligences()
    {
        var doc = File.ReadAllText(TestGameData.Load().Find("BOMB.DOC")!, Encoding.Latin1);
        var section = doc[doc.IndexOf("PLAYER SELECTION", StringComparison.Ordinal)..];
        // BOMB.DOC writes "Mr.  Stupid" with two spaces.
        foreach (var name in ComputerOpponents.Names.Skip(1))
            Assert.Contains(name.Replace("Mr. ", "Mr.  ", StringComparison.Ordinal), section, StringComparison.Ordinal);
    }
}
