using System;
using System.Linq;
using ConnectFour.Core.Synth;
using Xunit;

namespace ConnectFour.Tests;

public class ImpactSynthTests
{
	private static readonly ImpactRecipe Clack = new(
		BaseFrequency: 1900, ModeRatios: new[] { 1.0, 2.2, 3.7 }, ModeGains: new[] { 1.0, 0.5, 0.25 },
		Decay: 0.035, NoiseGain: 0.5, NoiseDecay: 0.004, NoiseBrightness: 0.5, Length: 0.3);

	[Fact]
	public void ImpactHasRequestedLengthAndPeak()
	{
		var samples = ImpactSynth.Impact(Clack, seed: 1);
		Assert.Equal((int)(0.3 * ImpactSynth.SampleRate), samples.Length);
		Assert.Equal(0.9f, samples.Max(Math.Abs), 3);
	}

	[Fact]
	public void ImpactDecaysOverTime()
	{
		var samples = ImpactSynth.Impact(Clack, seed: 2);
		int window = samples.Length / 10;
		double head = samples.Take(window).Sum(s => s * s);
		double tail = samples.Skip(samples.Length - window).Sum(s => s * s);
		Assert.True(tail < head * 0.01, $"tail energy {tail} should be far below head energy {head}");
	}

	[Fact]
	public void SameSeedIsDeterministicAndDifferentSeedsVary()
	{
		Assert.Equal(ImpactSynth.Impact(Clack, 7), ImpactSynth.Impact(Clack, 7));
		Assert.NotEqual(ImpactSynth.Impact(Clack, 7), ImpactSynth.Impact(Clack, 8));
	}

	[Fact]
	public void SlideLoopIsSeamless()
	{
		var loop = ImpactSynth.SlideLoop(brightness: 0.35, lowCut: 0.05, seconds: 1.0, seed: 3);
		float largestStep = 0f;
		for (int i = 1; i < loop.Length; i++)
			largestStep = Math.Max(largestStep, Math.Abs(loop[i] - loop[i - 1]));
		// The jump across the loop point should be no bigger than ordinary sample-to-sample motion.
		Assert.True(Math.Abs(loop[0] - loop[^1]) <= largestStep);
	}

	[Fact]
	public void Pcm16IsLittleEndianAndClamped()
	{
		var bytes = ImpactSynth.ToPcm16(new[] { 0f, 1f, -1f, 2f });
		Assert.Equal(new byte[] { 0x00, 0x00, 0xFF, 0x7F, 0x01, 0x80, 0xFF, 0x7F }, bytes);
	}
}
