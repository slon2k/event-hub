namespace EventHub.Infrastructure.UnitTests;

public class RsvpTokenServiceTests
{
    // A stable 32-byte key encoded as base64 — used across all tests
    private const string ValidKey = "dGVzdC1rZXktMzItYnl0ZXMtbG9uZy0hISEhISE="; // "test-key-32-bytes-long-!!!!!" padded

    private static RsvpTokenService CreateService() => new(ValidKey);

    private static (Guid InvitationId, string Email, DateTimeOffset ExpiresAt) DefaultInputs() =>
        (Guid.NewGuid(), "participant@example.com", DateTimeOffset.UtcNow.AddHours(72));

    // ── Generate ──────────────────────────────────────────────────────────────

    public class Generate
    {
        [Fact]
        public void ReturnsNonEmptyRawTokenAndHash()
        {
            var svc = CreateService();
            var (id, email, exp) = DefaultInputs();

            var (rawToken, tokenHash) = svc.Generate(id, email, exp);

            Assert.NotEmpty(rawToken);
            Assert.NotEmpty(tokenHash);
        }

        [Fact]
        public void RawTokenIsBase64Url_NoPlus_NoSlash_NoPadding()
        {
            var svc = CreateService();
            var (id, email, exp) = DefaultInputs();

            var (rawToken, _) = svc.Generate(id, email, exp);

            Assert.DoesNotContain("+", rawToken);
            Assert.DoesNotContain("/", rawToken);
            Assert.DoesNotContain("=", rawToken);
        }

        [Fact]
        public void TokenHash_IsLowercaseHex_64Chars()
        {
            var svc = CreateService();
            var (id, email, exp) = DefaultInputs();

            var (_, tokenHash) = svc.Generate(id, email, exp);

            Assert.Equal(64, tokenHash.Length);
            Assert.Matches("^[0-9a-f]{64}$", tokenHash);
        }

        [Fact]
        public void IsDeterministic_SameInputsProduceSameToken()
        {
            var svc = CreateService();
            var id = Guid.NewGuid();
            var email = "user@example.com";
            var exp = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);

            var (raw1, hash1) = svc.Generate(id, email, exp);
            var (raw2, hash2) = svc.Generate(id, email, exp);

            Assert.Equal(raw1, raw2);
            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void DifferentEmails_ProduceDifferentTokens()
        {
            var svc = CreateService();
            var id = Guid.NewGuid();
            var exp = DateTimeOffset.UtcNow.AddHours(72);

            var (raw1, _) = svc.Generate(id, "alice@example.com", exp);
            var (raw2, _) = svc.Generate(id, "bob@example.com", exp);

            Assert.NotEqual(raw1, raw2);
        }

        [Fact]
        public void DifferentInvitationIds_ProduceDifferentTokens()
        {
            var svc = CreateService();
            var email = "user@example.com";
            var exp = DateTimeOffset.UtcNow.AddHours(72);

            var (raw1, _) = svc.Generate(Guid.NewGuid(), email, exp);
            var (raw2, _) = svc.Generate(Guid.NewGuid(), email, exp);

            Assert.NotEqual(raw1, raw2);
        }

        [Fact]
        public void DifferentExpiry_ProduceDifferentTokens()
        {
            var svc = CreateService();
            var id = Guid.NewGuid();
            var email = "user@example.com";

            var (raw1, _) = svc.Generate(id, email, DateTimeOffset.UtcNow.AddHours(24));
            var (raw2, _) = svc.Generate(id, email, DateTimeOffset.UtcNow.AddHours(72));

            Assert.NotEqual(raw1, raw2);
        }

        [Fact]
        public void EmailIsCaseInsensitive_UpperAndLowerProduceSameToken()
        {
            var svc = CreateService();
            var id = Guid.NewGuid();
            var exp = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);

            var (raw1, _) = svc.Generate(id, "User@Example.COM", exp);
            var (raw2, _) = svc.Generate(id, "user@example.com", exp);

            Assert.Equal(raw1, raw2);
        }

        [Fact]
        public void DifferentKeys_ProduceDifferentTokens()
        {
            var key2 = Convert.ToBase64String(new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8,
                                                              9, 10, 11, 12, 13, 14, 15, 16,
                                                              17, 18, 19, 20, 21, 22, 23, 24,
                                                              25, 26, 27, 28, 29, 30, 31, 32 });
            var svc1 = CreateService();
            var svc2 = new RsvpTokenService(key2);
            var id = Guid.NewGuid();
            var email = "user@example.com";
            var exp = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);

            var (raw1, _) = svc1.Generate(id, email, exp);
            var (raw2, _) = svc2.Generate(id, email, exp);

            Assert.NotEqual(raw1, raw2);
        }
    }

    // ── IsValid ───────────────────────────────────────────────────────────────

    public class IsValid
    {
        [Fact]
        public void ValidToken_ReturnsTrue()
        {
            var svc = CreateService();
            var (id, email, exp) = DefaultInputs();
            var (rawToken, tokenHash) = svc.Generate(id, email, exp);

            var result = svc.IsValid(rawToken, tokenHash, exp);

            Assert.True(result);
        }

        [Fact]
        public void ExpiredToken_ReturnsFalse()
        {
            var svc = CreateService();
            var (id, email, _) = DefaultInputs();
            var pastExpiry = DateTimeOffset.UtcNow.AddSeconds(-1);
            var (rawToken, tokenHash) = svc.Generate(id, email, pastExpiry);

            var result = svc.IsValid(rawToken, tokenHash, pastExpiry);

            Assert.False(result);
        }

        [Fact]
        public void TamperedRawToken_ReturnsFalse()
        {
            var svc = CreateService();
            var (id, email, exp) = DefaultInputs();
            var (_, tokenHash) = svc.Generate(id, email, exp);

            var result = svc.IsValid("tampered-token-value", tokenHash, exp);

            Assert.False(result);
        }

        [Fact]
        public void WrongStoredHash_ReturnsFalse()
        {
            var svc = CreateService();
            var (id, email, exp) = DefaultInputs();
            var (rawToken, _) = svc.Generate(id, email, exp);
            var wrongHash = new string('a', 64); // valid-length but wrong hash

            var result = svc.IsValid(rawToken, wrongHash, exp);

            Assert.False(result);
        }

        [Fact]
        public void TokenFromDifferentKey_ReturnsFalse()
        {
            var key2 = Convert.ToBase64String(new byte[32]);
            var svc1 = CreateService();
            var svc2 = new RsvpTokenService(key2);
            var (id, email, exp) = DefaultInputs();
            var (rawToken, _) = svc1.Generate(id, email, exp);
            var (_, hashFromKey2) = svc2.Generate(id, email, exp);

            // rawToken was generated by svc1 but we validate it against svc2's hash
            var result = svc1.IsValid(rawToken, hashFromKey2, exp);

            Assert.False(result);
        }
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public class Constructor
    {
        [Fact]
        public void InvalidBase64Key_Throws()
        {
            Assert.Throws<FormatException>(() => new RsvpTokenService("not-valid-base64!!!"));
        }
    }
}
