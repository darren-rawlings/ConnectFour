using System;

namespace ConnectFour.Core.Synth;

/// <summary>
/// Describes how a material rings when struck: a few resonant modes (pitch ratios above the base
/// frequency) that decay exponentially, plus a short burst of filtered noise for the "click".
/// Metal and glass have long decays and inharmonic ratios; wood and stone are short and noisy.
/// </summary>
public sealed record ImpactRecipe(
	double BaseFrequency,
	double[] ModeRatios,
	double[] ModeGains,
	double Decay,
	double NoiseGain,
	double NoiseDecay,
	double NoiseBrightness,
	double Length);

/// <summary>Synthesises sound effects as raw samples so the game needs no licensed audio files.</summary>
public static class ImpactSynth
{
	public const int SampleRate = 44100;

	/// <summary>One impact sound, normalised to a peak of 0.9. Different seeds give natural variations.</summary>
	public static float[] Impact(ImpactRecipe recipe, int seed)
	{
		var rng = new Random(seed);
		int length = (int)(recipe.Length * SampleRate);
		var samples = new float[length];
		double detune = 1.0 + (rng.NextDouble() - 0.5) * 0.12;

		for (int mode = 0; mode < recipe.ModeRatios.Length; mode++)
		{
			double frequency = recipe.BaseFrequency * recipe.ModeRatios[mode] * detune
				* (1.0 + (rng.NextDouble() - 0.5) * 0.03);
			if (frequency > SampleRate * 0.45)
				continue;
			double gain = mode < recipe.ModeGains.Length ? recipe.ModeGains[mode] : 0.0;
			// Higher modes die away faster, which is what makes a strike sound "solid".
			double decay = recipe.Decay / (1.0 + mode * 0.7);
			double phase = rng.NextDouble() * Math.Tau;
			double step = Math.Tau * frequency / SampleRate;
			for (int i = 0; i < length; i++)
			{
				double t = (double)i / SampleRate;
				samples[i] += (float)(gain * Math.Exp(-t / decay) * Math.Sin(phase + step * i));
			}
		}

		double lowPassed = 0.0;
		for (int i = 0; i < length; i++)
		{
			double t = (double)i / SampleRate;
			lowPassed += recipe.NoiseBrightness * (rng.NextDouble() * 2.0 - 1.0 - lowPassed);
			samples[i] += (float)(recipe.NoiseGain * lowPassed * Math.Exp(-t / recipe.NoiseDecay));
		}

		ApplyEdges(samples, attack: 0.0004, release: 0.01);
		Normalize(samples, 0.9f);
		return samples;
	}

	/// <summary>Converts samples in [-1, 1] to little-endian signed 16-bit PCM.</summary>
	public static byte[] ToPcm16(float[] samples)
	{
		var bytes = new byte[samples.Length * 2];
		for (int i = 0; i < samples.Length; i++)
		{
			short value = (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
			bytes[i * 2] = (byte)(value & 0xFF);
			bytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
		}
		return bytes;
	}

	private static void ApplyEdges(float[] samples, double attack, double release)
	{
		int attackSamples = Math.Max(1, (int)(attack * SampleRate));
		int releaseSamples = Math.Max(1, (int)(release * SampleRate));
		for (int i = 0; i < attackSamples && i < samples.Length; i++)
			samples[i] *= (float)i / attackSamples;
		for (int i = 0; i < releaseSamples && i < samples.Length; i++)
			samples[samples.Length - 1 - i] *= (float)i / releaseSamples;
	}

	private static void Normalize(float[] samples, float peak)
	{
		float max = 0f;
		foreach (float s in samples)
			max = Math.Max(max, Math.Abs(s));
		if (max <= 0f)
			return;
		float scale = peak / max;
		for (int i = 0; i < samples.Length; i++)
			samples[i] *= scale;
	}
}
