using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Saesentsessis.Shaders.Stripping
{
	/// <summary>
	/// A set of keyword values of one <see cref="ShaderKeywordSchema"/>, one bit per value (up to <see cref="Capacity"/>).
	/// A variant sets one bit per keyword group of its pass; a pattern sets every value it allows.
	/// </summary>
	/// <remarks>Four <c>ulong</c> words, unmanaged and blittable, so Burst jobs evaluate it directly.</remarks>
	public struct VariantBits : IEquatable<VariantBits>
	{
		/// <summary>
		/// Most keyword values a schema can have.
		/// </summary>
		public const int Capacity = 256;

		public static VariantBits Empty => default;

		/// <summary>
		/// Every bit below <paramref name="count"/>.
		/// </summary>
		public static VariantBits FirstBits(int count)
		{
			var bits = default(VariantBits);
			for (var word = 0; word < 4; word++)
			{
				var inWord = math.clamp(count - word * 64, 0, 64);
				var mask = inWord == 64 ? ulong.MaxValue : (1UL << inWord) - 1UL;
				bits.SetWord(word, mask);
			}

			return bits;
		}

		public static VariantBits operator |(VariantBits a, VariantBits b) => new()
		{
			Word0 = a.Word0 | b.Word0,
			Word1 = a.Word1 | b.Word1,
			Word2 = a.Word2 | b.Word2,
			Word3 = a.Word3 | b.Word3
		};

		public static VariantBits operator &(VariantBits a, VariantBits b) => new()
		{
			Word0 = a.Word0 & b.Word0,
			Word1 = a.Word1 & b.Word1,
			Word2 = a.Word2 & b.Word2,
			Word3 = a.Word3 & b.Word3
		};

		public static VariantBits operator ~(VariantBits a) => new()
		{
			Word0 = ~a.Word0,
			Word1 = ~a.Word1,
			Word2 = ~a.Word2,
			Word3 = ~a.Word3
		};

		public static bool operator ==(VariantBits a, VariantBits b) => a.Equals(b);

		public static bool operator !=(VariantBits a, VariantBits b) => a.Equals(b) == false;

		public ulong Word0;
		public ulong Word1;
		public ulong Word2;
		public ulong Word3;

		public readonly bool IsEmpty => (Word0 | Word1 | Word2 | Word3) == 0UL;

		/// <summary>
		/// Whether every bit of this set is also in <paramref name="other"/>: a variant matches a pattern when it is a
		/// subset of it.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool IsSubsetOf(in VariantBits other)
			=> ((Word0 & ~other.Word0) | (Word1 & ~other.Word1) | (Word2 & ~other.Word2) | (Word3 & ~other.Word3)) == 0UL;

		/// <summary>
		/// Whether the two sets share a bit.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Overlaps(in VariantBits other)
			=> ((Word0 & other.Word0) | (Word1 & other.Word1) | (Word2 & other.Word2) | (Word3 & other.Word3)) != 0UL;

		public readonly bool Get(int bit) => (GetWord(bit >> 6) & (1UL << (bit & 63))) != 0UL;

		public void Set(int bit)
		{
			var word = bit >> 6;
			SetWord(word, GetWord(word) | (1UL << (bit & 63)));
		}

		public void Clear(int bit)
		{
			var word = bit >> 6;
			SetWord(word, GetWord(word) & ~(1UL << (bit & 63)));
		}

		/// <summary>
		/// The <paramref name="count"/> bits from <paramref name="start"/>, as a mask in the low bits (count at most 64,
		/// within one word: a <see cref="KeywordGroup"/> never crosses a word).
		/// </summary>
		public readonly ulong GetRange(int start, int count)
		{
			var mask = count == 64 ? ulong.MaxValue : (1UL << count) - 1UL;
			return (GetWord(start >> 6) >> (start & 63)) & mask;
		}

		/// <summary>
		/// Replaces the <paramref name="count"/> bits from <paramref name="start"/> with the low bits of
		/// <paramref name="value"/> (count at most 64, within one word).
		/// </summary>
		public void SetRange(int start, int count, ulong value)
		{
			var mask = count == 64 ? ulong.MaxValue : (1UL << count) - 1UL;
			var shift = start & 63;
			var word = start >> 6;
			SetWord(word, (GetWord(word) & ~(mask << shift)) | ((value & mask) << shift));
		}

		public readonly bool Equals(VariantBits other)
			=> Word0 == other.Word0 && Word1 == other.Word1 && Word2 == other.Word2 && Word3 == other.Word3;

		public override readonly bool Equals(object obj) => obj is VariantBits other && Equals(other);

		public override readonly int GetHashCode() => HashCode.Combine(Word0, Word1, Word2, Word3);

		public override readonly string ToString() => $"{Word3:X16}{Word2:X16}{Word1:X16}{Word0:X16}";

		private readonly ulong GetWord(int word) => word switch
		{
			0 => Word0,
			1 => Word1,
			2 => Word2,
			_ => Word3
		};

		private void SetWord(int word, ulong value)
		{
			switch (word)
			{
				case 0: Word0 = value; break;
				case 1: Word1 = value; break;
				case 2: Word2 = value; break;
				default: Word3 = value; break;
			}
		}
	}
}
