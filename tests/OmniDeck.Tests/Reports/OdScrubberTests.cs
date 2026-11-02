using OmniDeck.Core.Reports;

namespace OmniDeck.Tests.Reports;

public sealed class OdScrubberTests
{
    [Theory]
    [InlineData("GET https://discord.com/api/v9/users/@me?token=abc#frag done", "GET https://discord.com/api/v9/users/@me?[removed] done")]
    [InlineData("Cookie: __Secure-1PSID=abc; SID=def", "Cookie: [removed]")]
    [InlineData("set-cookie=session=xyz", "set-cookie=[removed]")]
    [InlineData("Authorization: Bearer abc.def-ghi", "Authorization: Bearer [removed]")]
    [InlineData("jwt eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.sig_nature here", "jwt [removed] here")]
    [InlineData("mail jane.doe+x@example.co.uk now", "mail [email] now")]
    [InlineData("channel 1234567890123456789 and 12345", "channel [id] and 12345")]
    [InlineData("hash 0123456789abcdef0123456789ABCDEF end", "hash [removed] end")]
    [InlineData("key AbCdEfGhIjKlMnOpQrStUvWxYz0123456789_-+/AbCd== end", "key [removed] end")]
    [InlineData(@"at C:\Users\someone\AppData\Local\x.log", @"at %USERPROFILE%\AppData\Local\x.log")]
    [InlineData("at c:/users/someone/x.log", "at %USERPROFILE%/x.log")]
    public void ValuesThatCouldIdentifyTheUser_AreTakenOut(string text, string scrubbed) => Assert.Equal(scrubbed, OdScrubber.Scrub(text));

    [Fact]
    public void TheUsersOwnProfileFolder_IsTakenOut() =>
        Assert.Equal(@"%USERPROFILE%\Documents", OdScrubber.Scrub(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Documents"));

    [Theory]
    [InlineData("2026-11-02 10:18:41.036 +07:00 [INF] OmniDeck.Web.Diagnostics.OdSignInHealth: Web profile discord loaded with 3 cookies (4 at the last close)")]
    [InlineData("OmniDeck 0.2.0-alpha.1 (a0b4734) starting")]
    [InlineData("| Discord | Idle | 3 min | 166 MB |")]
    [InlineData("https://www.youtube.com/feed/music")]
    public void OrdinaryLines_StayAsTheyAre(string line) => Assert.Equal(line, OdScrubber.Scrub(line));
}
