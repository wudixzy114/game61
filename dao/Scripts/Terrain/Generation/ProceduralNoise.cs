using System;
using Godot;

namespace Dao.Terrain.Generation;

public static class ProceduralNoise
{
    public static float Value(float x, float y, int seed)
    {
        int xi = FastFloor(x);
        int yi = FastFloor(y);
        float tx = x - xi;
        float ty = y - yi;
        float sx = Smooth(tx);
        float sy = Smooth(ty);

        float a = Hash01(xi, yi, seed);
        float b = Hash01(xi + 1, yi, seed);
        float c = Hash01(xi, yi + 1, seed);
        float d = Hash01(xi + 1, yi + 1, seed);

        return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
    }

    public static float SignedValue(float x, float y, int seed)
    {
        return (Value(x, y, seed) * 2.0f) - 1.0f;
    }

    public static float Fbm(float x, float y, int seed, int octaves, float lacunarity = 2.03f, float gain = 0.5f)
    {
        float sum = 0.0f;
        float amplitude = 0.5f;
        float frequency = 1.0f;
        float normalization = 0.0f;

        for (int i = 0; i < octaves; i++)
        {
            sum += SignedValue(x * frequency, y * frequency, seed + i * 1013) * amplitude;
            normalization += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return normalization <= 0.0f ? 0.0f : sum / normalization;
    }

    public static float Ridged(float x, float y, int seed, int octaves)
    {
        float sum = 0.0f;
        float amplitude = 0.5f;
        float frequency = 1.0f;
        float normalization = 0.0f;

        for (int i = 0; i < octaves; i++)
        {
            float n = SignedValue(x * frequency, y * frequency, seed + i * 1619);
            n = 1.0f - Mathf.Abs(n);
            n *= n;
            sum += n * amplitude;
            normalization += amplitude;
            amplitude *= 0.53f;
            frequency *= 2.11f;
        }

        return normalization <= 0.0f ? 0.0f : sum / normalization;
    }

    public static Vector2 DomainWarp(Vector2 position, float scale, float amplitude, int seed)
    {
        float sx = position.X / scale;
        float sy = position.Y / scale;
        float wx = Fbm(sx, sy, seed + 37, 4);
        float wy = Fbm(sx + 19.17f, sy - 4.73f, seed + 73, 4);
        return new Vector2(position.X + wx * amplitude, position.Y + wy * amplitude);
    }

    public static float Terrace(float height, float stepSize, float strength)
    {
        if (stepSize <= 0.001f || strength <= 0.001f)
        {
            return height;
        }

        float stepped = Mathf.Round(height / stepSize) * stepSize;
        return Mathf.Lerp(height, stepped, Mathf.Clamp(strength, 0.0f, 1.0f));
    }

    private static int FastFloor(float value)
    {
        int i = (int)value;
        return value < i ? i - 1 : i;
    }

    private static float Smooth(float t)
    {
        return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f);
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 0x9E3779B9u;
            h = RotateLeft(h, 13);
            h ^= (uint)y * 0x85EBCA6Bu;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215.0f;
        }
    }

    private static uint RotateLeft(uint value, int offset)
    {
        return (value << offset) | (value >> (32 - offset));
    }
}
