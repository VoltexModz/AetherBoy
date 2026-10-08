using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;

namespace AetherBoy.RuntimeTests;

[TestClass]
public class CheatInputReviewTests
{
    [TestMethod]
    [DataRow("A6A339F5 FC0ADC79")]
    [DataRow("BEEB5AC7 3EAF7918")]
    [DataRow("98A631EF C20F30E4")]
    public void AutomaticIgnoresCoincidentalRawInterpretations(string code)
    {
        var program = GbaCheatProgram.Compile(code);
        StringAssert.StartsWith(program.Code, "AR3:");
        Assert.IsFalse(CheatCodeInput.Review(code, true).HasErrors);
    }

    [TestMethod]
    public void TrueAmbiguityOffersOnlyMatchingDevicesWithoutPublishing()
    {
        var engine = new GbaCheatEngine();
        var error = Assert.ThrowsExactly<FormatException>(() => engine.Add("ambiguous", "C12BBBE1 D1ED426C"));
        CollectionAssert.AreEqual(new[] { CheatCodeFormat.GameShark, CheatCodeFormat.ActionReplayV3 }, CheatCodeInput.Candidates(error).ToArray());
        Assert.IsEmpty(engine.CaptureSnapshots());
        var review = CheatCodeInput.Review("C12BBBE1 D1ED426C", true);
        Assert.IsTrue(review.HasErrors); Assert.HasCount(2, review.Candidates);
        Assert.IsFalse(CheatCodeInput.Review("C12BBBE1 D1ED426C", true, CheatCodeFormat.ActionReplayV3).HasErrors);
        Assert.IsFalse(CheatCodeInput.Review("GSRAW:02000000 00000001", true).HasErrors);
        Assert.IsFalse(CheatCodeInput.Review("00200000 00000001", true, CheatCodeFormat.ActionReplayV3Raw).HasErrors);
    }

    [TestMethod]
    [DataRow("95EDFBBA A5A72A78\r\n\r\nC833D1A0 02FA7205")]
    [DataRow("\t95EDFBBA\u00a0A5A72A78\nC833D1A0\t02FA7205 ")]
    [DataRow("4202462CFFFF\n0000003C0002")]
    public void BrowserWhitespaceAndCompactCodesRemainAccepted(string code)
    {
        Assert.IsFalse(CheatCodeInput.Review(code, true).HasErrors);
        Assert.AreEqual(code.Trim(), CheatCodeInput.Prepare(code, CheatCodeFormat.Automatic));
    }

    [TestMethod]
    [DataRow("Cheat code:\r\n95EDFBBA A5A72A78\r\nC833D1A0 02FA7205", 1)]
    [DataRow("\n83007CEE 0090 = Articuno", 2)]
    [DataRow("3AF85ACA C4D18CEC\n\n8E883EFF 92E9660Dsvg", 3)]
    [DataRow("95EDFBBA\u200b A5A72A78\nC833D1A0 02FA7205", 1)]
    [DataRow("1003DAE6 0007 + 83007D12 0ZZZ + 33007D14 00YY", 2)]
    public void ReviewPointsToUnmodifiedProblemLines(string code, int expectedLine)
    {
        var review = CheatCodeInput.Review(code, true);
        Assert.IsTrue(review.HasErrors);
        Assert.AreEqual(expectedLine, review.Issues[0].Line);
        foreach (var line in review.Lines) Assert.AreEqual(code.Substring(line.Start, line.Length), line.Text);
        Assert.AreEqual(code.Trim(), CheatCodeInput.Prepare(code, CheatCodeFormat.Automatic));
    }

    [TestMethod]
    public void HexAaaaRequiresUiConfirmationButIsNotAnInvalidCode()
    {
        var review = CheatCodeInput.Review("82025840 AAAA", true);
        Assert.IsFalse(review.HasErrors); Assert.IsTrue(review.NeedsValueConfirmation);
        Assert.IsFalse(CheatCodeInput.Review("82025840 0044", true).NeedsValueConfirmation);
        Assert.IsFalse(CheatCodeInput.Review("82025840 FFFF", true).NeedsValueConfirmation);
    }

    [TestMethod]
    public void ReviewUsesMasterContextOnCopiesOnly()
    {
        const string master = "928817AD 553B\n540555A6 779B\n374D7A76 7115";
        const string code = "44645C94 C2DC\n4D741CC0 F04D\n4551D1E7 541F\nFA5AF752 09B0";
        Assert.IsTrue(CheatCodeInput.Review(code, true).HasErrors);
        Assert.IsFalse(CheatCodeInput.Review(code, true, previousSets: [master]).HasErrors);
        Assert.IsTrue(CheatCodeInput.Review(code, true).HasErrors, "Review must not retain a master between sessions.");
        Assert.IsTrue(CheatCodeInput.Review(new string('F', 32769), true).HasErrors);
        Assert.IsTrue(CheatCodeInput.Review(string.Join('\n', Enumerable.Repeat("82025840 0044", 513)), true).HasErrors);
    }

    [TestMethod]
    [DataRow("01FF00C0")]
    [DataRow("00A000-FF")]
    [DataRow("C000:FF")]
    [DataRow("123-456-789")]
    public void GameBoyAndColorInputsKeepTheirExistingSyntax(string code) => Assert.IsFalse(CheatCodeInput.Review(code, false).HasErrors);

    [TestMethod]
    public void SemanticFailureRetainsSourceLineAcrossEmptyLines()
    {
        var review = CheatCodeInput.Review("CB:82000000 0001\n\n88000000 0001", true);
        Assert.IsTrue(review.HasErrors); Assert.AreEqual(3, review.Issues[0].Line);
    }

    [TestMethod]
    public void RemovedMasterDoesNotMakePreviewRejectAnExistingRuntimeContext()
    {
        var engine = new GbaCheatEngine();
        var master = engine.Add("master", "CB:9ABCDEF0 1234");
        engine.Add("body", "CB:932C7D22 AC8D");
        engine.Remove(master.Id);
        var review = CheatCodeInput.Review("CB:932C7D22 AC8D", true,
            previousSets: engine.CaptureSnapshots().Select(c => c.Code));
        Assert.IsFalse(review.HasErrors); Assert.IsTrue(review.RequiresSessionValidation);
        engine.Add("second body", "CB:932C7D22 AC8D");
        Assert.HasCount(2, engine.CaptureSnapshots());
    }
}
