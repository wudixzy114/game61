#include "dao_extension.h"

#include <algorithm>
#include <cmath>
#include <cstdint>

#include <godot_cpp/core/class_db.hpp>

namespace godot {

namespace {

struct NativeTerrainProfile {
	int32_t seed = 613061;
	double height_scale = 780.0;
	double continent_scale = 5200.0;
	double mountain_scale = 1800.0;
	double mountain_weight = 0.72;
	double valley_weight = 0.44;
	double detail_weight = 0.18;
	double vista_frequency = 0.42;
	double river_strength = 0.58;
	double river_carve_depth = 115.0;
	double terrace_strength = 72.0;
};

int fast_floor(double p_value) {
	int i = static_cast<int>(p_value);
	return p_value < static_cast<double>(i) ? i - 1 : i;
}

uint32_t rotate_left(uint32_t p_value, int p_offset) {
	return (p_value << p_offset) | (p_value >> (32 - p_offset));
}

double smooth(double p_t) {
	return p_t * p_t * p_t * (p_t * (p_t * 6.0 - 15.0) + 10.0);
}

double hash_01(int p_x, int p_y, int32_t p_seed) {
	uint32_t h = static_cast<uint32_t>(p_seed);
	h ^= static_cast<uint32_t>(p_x) * 0x9E3779B9u;
	h = rotate_left(h, 13);
	h ^= static_cast<uint32_t>(p_y) * 0x85EBCA6Bu;
	h *= 0xC2B2AE35u;
	h ^= h >> 16;
	return static_cast<double>(h & 0x00FFFFFFu) / 16777215.0;
}

double value_noise(double p_x, double p_y, int32_t p_seed) {
	const int xi = fast_floor(p_x);
	const int yi = fast_floor(p_y);
	const double tx = p_x - static_cast<double>(xi);
	const double ty = p_y - static_cast<double>(yi);
	const double sx = smooth(tx);
	const double sy = smooth(ty);

	const double a = hash_01(xi, yi, p_seed);
	const double b = hash_01(xi + 1, yi, p_seed);
	const double c = hash_01(xi, yi + 1, p_seed);
	const double d = hash_01(xi + 1, yi + 1, p_seed);
	const double ab = a + (b - a) * sx;
	const double cd = c + (d - c) * sx;
	return ab + (cd - ab) * sy;
}

double signed_value_noise(double p_x, double p_y, int32_t p_seed) {
	return value_noise(p_x, p_y, p_seed) * 2.0 - 1.0;
}

double fbm(double p_x, double p_y, int32_t p_seed, int p_octaves, double p_lacunarity = 2.03, double p_gain = 0.5) {
	double sum = 0.0;
	double amplitude = 0.5;
	double frequency = 1.0;
	double normalization = 0.0;

	for (int i = 0; i < p_octaves; i++) {
		sum += signed_value_noise(p_x * frequency, p_y * frequency, p_seed + i * 1013) * amplitude;
		normalization += amplitude;
		amplitude *= p_gain;
		frequency *= p_lacunarity;
	}

	return normalization <= 0.0 ? 0.0 : sum / normalization;
}

double ridged(double p_x, double p_y, int32_t p_seed, int p_octaves) {
	double sum = 0.0;
	double amplitude = 0.5;
	double frequency = 1.0;
	double normalization = 0.0;

	for (int i = 0; i < p_octaves; i++) {
		double n = signed_value_noise(p_x * frequency, p_y * frequency, p_seed + i * 1619);
		n = 1.0 - std::abs(n);
		n *= n;
		sum += n * amplitude;
		normalization += amplitude;
		amplitude *= 0.53;
		frequency *= 2.11;
	}

	return normalization <= 0.0 ? 0.0 : sum / normalization;
}

double clamp_value(double p_value, double p_min, double p_max) {
	return std::max(p_min, std::min(p_max, p_value));
}

double smooth_step(double p_from, double p_to, double p_value) {
	if (p_to <= p_from) {
		return p_value < p_from ? 0.0 : 1.0;
	}

	const double x = clamp_value((p_value - p_from) / (p_to - p_from), 0.0, 1.0);
	return x * x * (3.0 - 2.0 * x);
}

double terrace(double p_height, double p_step_size, double p_strength) {
	if (p_step_size <= 0.001 || p_strength <= 0.001) {
		return p_height;
	}

	const double stepped = std::round(p_height / p_step_size) * p_step_size;
	return p_height + (stepped - p_height) * clamp_value(p_strength, 0.0, 1.0);
}

void domain_warp(double p_x, double p_z, double p_scale, double p_amplitude, int32_t p_seed, double &r_x, double &r_z) {
	const double sx = p_x / p_scale;
	const double sz = p_z / p_scale;
	const double wx = fbm(sx, sz, p_seed + 37, 4);
	const double wz = fbm(sx + 19.17, sz - 4.73, p_seed + 73, 4);
	r_x = p_x + wx * p_amplitude;
	r_z = p_z + wz * p_amplitude;
}

double sample_height_native(double p_x, double p_z, const NativeTerrainProfile &p_profile) {
	double warped_x = 0.0;
	double warped_z = 0.0;
	domain_warp(
			p_x,
			p_z,
			p_profile.continent_scale * 0.42,
			p_profile.continent_scale * 0.085,
			p_profile.seed,
			warped_x,
			warped_z);

	double continent = fbm(
			warped_x / p_profile.continent_scale,
			warped_z / p_profile.continent_scale,
			p_profile.seed + 11,
			6);
	continent = clamp_value((continent + 1.0) * 0.5, 0.0, 1.0);

	const double basin = smooth_step(0.18, 0.82, continent);
	const double shelf = smooth_step(0.35, 0.72, continent);

	double mountain_warp_x = 0.0;
	double mountain_warp_z = 0.0;
	domain_warp(
			p_x,
			p_z,
			p_profile.mountain_scale * 0.62,
			p_profile.mountain_scale * 0.11,
			p_profile.seed + 199,
			mountain_warp_x,
			mountain_warp_z);
	const double ridge = ridged(
			mountain_warp_x / p_profile.mountain_scale,
			mountain_warp_z / p_profile.mountain_scale,
			p_profile.seed + 29,
			7);
	const double mountain_mask = smooth_step(0.42, 0.86, continent);
	const double mountains = ridge * mountain_mask * p_profile.mountain_weight;

	const double broad = fbm(
								 warped_x / (p_profile.mountain_scale * 1.75),
								 warped_z / (p_profile.mountain_scale * 1.75),
								 p_profile.seed + 41,
								 5) *
					0.5 +
			0.5;

	const double canyon_noise = ridged(
			(warped_x + 811.0) / (p_profile.mountain_scale * 0.82),
			(warped_z - 347.0) / (p_profile.mountain_scale * 0.82),
			p_profile.seed + 53,
			5);
	const double main_river = 1.0 - smooth_step(0.035, 0.215, std::abs(canyon_noise - 0.52));
	const double tributary_noise = ridged(
			(warped_x - 1729.0) / (p_profile.mountain_scale * 1.34),
			(warped_z + 941.0) / (p_profile.mountain_scale * 1.34),
			p_profile.seed + 137,
			4);
	const double tributary = 1.0 - smooth_step(0.03, 0.18, std::abs(tributary_noise - 0.48));
	double river = std::max(main_river, tributary * 0.58);
	river = clamp_value(river * smooth_step(0.21, 0.72, continent) * p_profile.river_strength * 1.24, 0.0, 1.0);

	const double micro = fbm(
			p_x / 118.0,
			p_z / 118.0,
			p_profile.seed + 71,
			4);

	double height =
			((basin - 0.44) * p_profile.height_scale * 0.72) +
			(shelf * broad * p_profile.height_scale * 0.34) +
			(mountains * p_profile.height_scale * 1.08) +
			(micro * p_profile.height_scale * p_profile.detail_weight);

	const double valley_carve = river * p_profile.river_carve_depth * (0.35 + mountains * 0.85);
	height -= valley_carve * p_profile.valley_weight;

	const double terrace_mask = smooth_step(0.52, 0.86, mountains) * p_profile.vista_frequency;
	return terrace(height, std::max(12.0, p_profile.terrace_strength), terrace_mask * 0.38);
}

NativeTerrainProfile make_profile(
		int32_t p_seed,
		double p_height_scale,
		double p_continent_scale,
		double p_mountain_scale,
		double p_mountain_weight,
		double p_valley_weight,
		double p_detail_weight,
		double p_vista_frequency,
		double p_river_strength,
		double p_river_carve_depth,
		double p_terrace_strength) {
	NativeTerrainProfile profile;
	profile.seed = p_seed;
	profile.height_scale = p_height_scale;
	profile.continent_scale = std::max(128.0, p_continent_scale);
	profile.mountain_scale = std::max(64.0, p_mountain_scale);
	profile.mountain_weight = clamp_value(p_mountain_weight, 0.0, 1.0);
	profile.valley_weight = clamp_value(p_valley_weight, 0.0, 1.0);
	profile.detail_weight = clamp_value(p_detail_weight, 0.0, 1.0);
	profile.vista_frequency = clamp_value(p_vista_frequency, 0.0, 1.0);
	profile.river_strength = clamp_value(p_river_strength, 0.0, 1.0);
	profile.river_carve_depth = p_river_carve_depth;
	profile.terrace_strength = p_terrace_strength;
	return profile;
}

} // namespace

void DaoExtension::_bind_methods() {
	ClassDB::bind_static_method("DaoExtension", D_METHOD("health_check"), &DaoExtension::health_check);
	ClassDB::bind_static_method(
			"DaoExtension",
			D_METHOD(
					"sample_height",
					"x",
					"z",
					"seed",
					"height_scale",
					"continent_scale",
					"mountain_scale",
					"mountain_weight",
					"valley_weight",
					"detail_weight",
					"vista_frequency",
					"river_strength",
					"river_carve_depth",
					"terrace_strength"),
			&DaoExtension::sample_height);
	ClassDB::bind_static_method(
			"DaoExtension",
			D_METHOD(
					"sample_height_grid",
					"seed",
					"origin_x",
					"origin_z",
					"resolution",
					"chunk_size",
					"height_scale",
					"continent_scale",
					"mountain_scale",
					"mountain_weight",
					"valley_weight",
					"detail_weight",
					"vista_frequency",
					"river_strength",
					"river_carve_depth",
					"terrace_strength"),
			&DaoExtension::sample_height_grid);
}

String DaoExtension::health_check() {
	return "dao gdextension loaded";
}

double DaoExtension::sample_height(
		double p_x,
		double p_z,
		int32_t p_seed,
		double p_height_scale,
		double p_continent_scale,
		double p_mountain_scale,
		double p_mountain_weight,
		double p_valley_weight,
		double p_detail_weight,
		double p_vista_frequency,
		double p_river_strength,
		double p_river_carve_depth,
		double p_terrace_strength) {
	const NativeTerrainProfile profile = make_profile(
			p_seed,
			p_height_scale,
			p_continent_scale,
			p_mountain_scale,
			p_mountain_weight,
			p_valley_weight,
			p_detail_weight,
			p_vista_frequency,
			p_river_strength,
			p_river_carve_depth,
			p_terrace_strength);
	return sample_height_native(p_x, p_z, profile);
}

PackedFloat32Array DaoExtension::sample_height_grid(
		int32_t p_seed,
		double p_origin_x,
		double p_origin_z,
		int32_t p_resolution,
		double p_chunk_size,
		double p_height_scale,
		double p_continent_scale,
		double p_mountain_scale,
		double p_mountain_weight,
		double p_valley_weight,
		double p_detail_weight,
		double p_vista_frequency,
		double p_river_strength,
		double p_river_carve_depth,
		double p_terrace_strength) {
	const int resolution = std::max(1, std::min(512, p_resolution));
	const int width = resolution + 1;
	const double step = p_chunk_size / static_cast<double>(resolution);
	const NativeTerrainProfile profile = make_profile(
			p_seed,
			p_height_scale,
			p_continent_scale,
			p_mountain_scale,
			p_mountain_weight,
			p_valley_weight,
			p_detail_weight,
			p_vista_frequency,
			p_river_strength,
			p_river_carve_depth,
			p_terrace_strength);

	PackedFloat32Array heights;
	heights.resize(width * width);

	for (int z = 0; z < width; z++) {
		for (int x = 0; x < width; x++) {
			const int index = z * width + x;
			const double world_x = p_origin_x + static_cast<double>(x) * step;
			const double world_z = p_origin_z + static_cast<double>(z) * step;
			heights.set(index, sample_height_native(world_x, world_z, profile));
		}
	}

	return heights;
}

extern "C" GDE_EXPORT int dao_native_sample_height_grid(
		int32_t p_seed,
		double p_origin_x,
		double p_origin_z,
		int32_t p_resolution,
		double p_chunk_size,
		double p_height_scale,
		double p_continent_scale,
		double p_mountain_scale,
		double p_mountain_weight,
		double p_valley_weight,
		double p_detail_weight,
		double p_vista_frequency,
		double p_river_strength,
		double p_river_carve_depth,
		double p_terrace_strength,
		float *r_output_heights,
		int32_t p_output_height_count) {
	const int resolution = std::max(1, std::min(512, p_resolution));
	const int width = resolution + 1;
	const int required_count = width * width;

	if (r_output_heights == nullptr || p_output_height_count < required_count) {
		return -required_count;
	}

	const double step = p_chunk_size / static_cast<double>(resolution);
	const NativeTerrainProfile profile = make_profile(
			p_seed,
			p_height_scale,
			p_continent_scale,
			p_mountain_scale,
			p_mountain_weight,
			p_valley_weight,
			p_detail_weight,
			p_vista_frequency,
			p_river_strength,
			p_river_carve_depth,
			p_terrace_strength);

	for (int z = 0; z < width; z++) {
		for (int x = 0; x < width; x++) {
			const int index = z * width + x;
			const double world_x = p_origin_x + static_cast<double>(x) * step;
			const double world_z = p_origin_z + static_cast<double>(z) * step;
			r_output_heights[index] = static_cast<float>(sample_height_native(world_x, world_z, profile));
		}
	}

	return required_count;
}

} // namespace godot
