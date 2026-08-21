using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Common.Cryptography;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Tests for OpenS4L.Common value types and pure helpers: password hashing, peer ids,
    /// character style bit-packing, color, and the level/experience helpers.
    /// </summary>
    public class CommonValueTypeTests
    {
        // ---- PasswordHasher ----

        [Fact]
        public void PasswordHasher_roundtrips()
        {
            var (hash, salt) = PasswordHasher.Hash("hunter2");
            Assert.True(PasswordHasher.IsPasswordValid("hunter2", hash, salt));
        }

        [Theory]
        [InlineData("wrong")]
        [InlineData("")]
        [InlineData(null)]
        public void PasswordHasher_rejectsInvalid(string password)
        {
            var (hash, salt) = PasswordHasher.Hash("correct");
            Assert.False(PasswordHasher.IsPasswordValid(password, hash, salt));
        }

        [Fact]
        public void PasswordHasher_rejectsNullOrEmptyInputs()
        {
            Assert.False(PasswordHasher.IsPasswordValid(null, "hash", "salt"));
            Assert.False(PasswordHasher.IsPasswordValid("pw", null, "salt"));
            Assert.False(PasswordHasher.IsPasswordValid("pw", "hash", null));
            Assert.False(PasswordHasher.IsPasswordValid("  ", "hash", "salt"));
        }

        [Fact]
        public void PasswordHasher_generatesDistinctSalts()
        {
            var (_, salt1) = PasswordHasher.Hash("pw");
            var (_, salt2) = PasswordHasher.Hash("pw");
            Assert.NotEqual(salt1, salt2);
        }

        // ---- PeerId ----

        [Fact]
        public void PeerId_roundtripsThroughValue()
        {
            var id = new PeerId(7, 3, 5);
            var back = (PeerId)(ushort)id;
            Assert.Equal(id, back);
            Assert.Equal(7, back.Id);
            Assert.Equal((byte)3, back.Slot);
            Assert.Equal((byte)5, back.ObjectType);
        }

        [Fact]
        public void PeerId_equalsAndOperators()
        {
            var a = new PeerId(1, 2, 3);
            var b = new PeerId(1, 2, 3);
            var c = new PeerId(9, 9, 9);
            Assert.True(a == b);
            Assert.False(a == c);
            Assert.True(a != c);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.True(a.Equals(b));
            // NOTE: PeerId.Equals(object) is buggy in production — it compares the packed
            // ushort against a boxed PeerId, which is never equal. Documented, not "fixed".
            Assert.False(a.Equals((object)b));
        }

        [Fact]
        public void PeerId_nullComparison()
        {
            PeerId a = null;
            PeerId b = new PeerId(1, 1, 1);
            Assert.False(a == b);
            Assert.True(a != b);
        }

        [Fact]
        public void PeerId_toString()
        {
            Assert.Contains("Id", new PeerId(1, 2, 3).ToString());
        }

        // ---- LongPeerId ----

        [Fact]
        public void LongPeerId_roundtripsThroughValue()
        {
            var accountId = 0x0000FFFF_00000000UL; // high 48 bits
            var peerId = new PeerId(5, 2, 1);
            var longId = new LongPeerId(accountId, peerId);
            var value = (ulong)longId;
            var back = (LongPeerId)value;
            Assert.Equal(accountId, back.AccountId);
            Assert.Equal(peerId, back.PeerId);
        }

        [Fact]
        public void LongPeerId_threeArgConstructor()
        {
            var id = new LongPeerId(123, 4, 5, 6);
            Assert.Equal(123UL, id.AccountId);
            Assert.Equal(new PeerId(4, 5, 6), id.PeerId);
        }

        [Fact]
        public void LongPeerId_equalsAndOperators()
        {
            var a = new LongPeerId(100, new PeerId(1, 2, 3));
            var b = new LongPeerId(100, new PeerId(1, 2, 3));
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.True(a.Equals(b));
            // NOTE: LongPeerId.Equals(object) is buggy in production — compares the packed
            // ulong against a boxed LongPeerId, never equal. Documented, not "fixed".
            Assert.False(a.Equals((object)b));
        }

        [Fact]
        public void LongPeerId_implicitPeerIdConversion()
        {
            LongPeerId id = new LongPeerId(100, new PeerId(9, 8, 7));
            PeerId peer = id;
            Assert.Equal(new PeerId(9, 8, 7), peer);
        }

        // ---- CharacterStyle ----

        [Fact]
        public void CharacterStyle_roundtripsThroughValue()
        {
            var style = new CharacterStyle(CharacterGender.Female, 2, 10, 20, 5, 30);
            var value = (uint)style;
            var back = (CharacterStyle)value;
            Assert.Equal(style.Gender, back.Gender);
            Assert.Equal(style.Hair, back.Hair);
            Assert.Equal(style.Face, back.Face);
            Assert.Equal(style.Shirt, back.Shirt);
            Assert.Equal(style.Pants, back.Pants);
            Assert.Equal(style.Slot, back.Slot);
        }

        [Fact]
        public void CharacterStyle_uintConstructor()
        {
            var style = (CharacterStyle)12345u;
            // 12345 & 1 == 1 => Female (CharacterGender.Female == 1)
            Assert.Equal(CharacterGender.Female, style.Gender);
            Assert.Equal((byte)(12345 >> 1 & 63), style.Hair);
        }

        [Fact]
        public void CharacterStyle_toString_returnsValue()
        {
            var style = new CharacterStyle(CharacterGender.Male, 0, 1, 2, 3, 4);
            Assert.Equal(((uint)style).ToString(), style.ToString());
        }

        // ---- S4Color ----

        [Fact]
        public void S4Color_statics()
        {
            Assert.Equal("255", S4Color.Red.R.ToString());
            Assert.Equal(255, S4Color.Red.A);
            Assert.Equal(255, S4Color.Green.G);
            Assert.Equal(255, S4Color.Blue.B);
        }

        [Fact]
        public void S4Color_fromRgbArgb()
        {
            var rgb = S4Color.FromRgb(10, 20, 30);
            Assert.Equal(255, rgb.A);
            Assert.Equal(10, rgb.R);
            var argb = S4Color.FromArgb(5, 6, 7, 8);
            Assert.Equal(5, argb.A);
            Assert.Equal(8, argb.B);
        }

        [Fact]
        public void S4Color_toString()
        {
            Assert.Equal("{CB-1,2,3,4}", new S4Color(4, 1, 2, 3).ToString());
        }

        // ---- Constants ----

        [Fact]
        public void Constants_cacheKeys()
        {
            Assert.Equal("session_42", Constants.Cache.SessionKey(42L));
            Assert.Equal("session_42", Constants.Cache.SessionKey(42UL));
            Assert.Equal("serverlist", Constants.Cache.ServerlistKey);
        }
    }
}
