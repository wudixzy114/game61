#include "dao_extension.h"

#include <algorithm>
#include <cmath>
#include <cstdint>

#include <godot_cpp/core/class_db.hpp>

namespace godot {

namespace {

struct NativeTerrainProfile {
	int32_t seed = 613061;
	double chunk_size = 192.0;
	double height_scale = 780.0;
	double sea_level = -18.0;
	double continent_scale = 5200.0;
	double mountain_scale = 1800.0;
	double mountain_weight = 0.72;
	double valley_weight = 0.44;
	double detail_weight = 0.18;
	double vista_frequency = 0.42;
	double river_strength = 0.58;
	double river_carve_depth = 115.0;
	double terrace_strength = 72.0;
	double land_balance_offset = 0.0;
};

struct NativeTerrainTerms {
	double continent = 0.0;
	double basin = 0.0;
	double shelf = 0.0;
	double mountains = 0.0;
	double broad_elevation = 0.0;
	double river = 0.0;
	double base_moisture = 0.0;
	double base_temperature = 0.0;
	double aridity = 0.0;
	double plains = 0.0;
	double wetland = 0.0;
	double forest = 0.0;
	double hills = 0.0;
	double alpine = 0.0;
	double island = 0.0;
	double dune_detail = 0.0;
};

enum NativeTerrainLandscapeKind {
	LANDSCAPE_OCEAN = 0,
	LANDSCAPE_COAST = 1,
	LANDSCAPE_LOWLAND = 2,
	LANDSCAPE_WETLAND = 3,
	LANDSCAPE_FOREST_BASIN = 4,
	LANDSCAPE_RIVER_VALLEY = 5,
	LANDSCAPE_CANYON = 6,
	LANDSCAPE_HIGHLANDS = 7,
	LANDSCAPE_MOUNTAIN_MASSIF = 8,
	LANDSCAPE_SNOWFIELD = 9,
	LANDSCAPE_VISTA_PLATEAU = 10,
};

enum NativeTerrainBiomeKind {
	BIOME_OCEAN = 0,
	BIOME_COAST = 1,
	BIOME_ISLAND = 2,
	BIOME_PLAINS = 3,
	BIOME_GRASSLAND = 4,
	BIOME_DESERT = 5,
	BIOME_OASIS = 6,
	BIOME_FOREST = 7,
	BIOME_WETLAND = 8,
	BIOME_HILLS = 9,
	BIOME_MOUNTAINS = 10,
	BIOME_SNOWFIELD = 11,
};

struct NativeTerrainField {
	double height = 0.0;
	double continent = 0.0;
	double basin = 0.0;
	double shelf = 0.0;
	double mountains = 0.0;
	double broad_elevation = 0.0;
	double river = 0.0;
	double moisture = 0.0;
	double temperature = 0.0;
	double scenic_potential = 0.0;
	double traversability = 0.0;
	double exposure = 0.0;
	double resource_potential = 0.0;
	double hazard_potential = 0.0;
	double encounter_potential = 0.0;
	int biome_kind = BIOME_GRASSLAND;
	int landscape_kind = LANDSCAPE_LOWLAND;
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

double lerp_value(double p_from, double p_to, double p_weight) {
	return p_from + (p_to - p_from) * p_weight;
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

double sample_height_unbalanced(
		double p_x,
		double p_z,
		const NativeTerrainProfile &p_profile,
		bool p_include_micro,
		double &r_mountains,
		double &r_alpine,
		NativeTerrainTerms *r_terms = nullptr) {
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

	const double island_noise = ridged(
			(warped_x + 2509.0) / (p_profile.continent_scale * 0.46),
			(warped_z - 1877.0) / (p_profile.continent_scale * 0.46),
			p_profile.seed + 233,
			4);
	const double island = smooth_step(0.63, 0.86, island_noise) *
			(1.0 - smooth_step(0.38, 0.58, continent));

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
	r_mountains = mountains;

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

	double climate_warp_x = 0.0;
	double climate_warp_z = 0.0;
	domain_warp(
			p_x,
			p_z,
			p_profile.continent_scale * 0.58,
			p_profile.continent_scale * 0.075,
			p_profile.seed + 307,
			climate_warp_x,
			climate_warp_z);
	const double base_moisture = clamp_value(
			fbm(
					(climate_warp_x - 1301.0) / 1350.0,
					(climate_warp_z + 661.0) / 1350.0,
					p_profile.seed + 83,
					5) *
							0.5 +
					0.5,
			0.0,
			1.0);
	const double latitude = std::abs(std::sin(p_z / 9000.0));
	const double temperature_noise = fbm(
			(climate_warp_x + 379.0) / 4200.0,
			(climate_warp_z - 919.0) / 4200.0,
			p_profile.seed + 317,
			4);
	const double base_temperature = clamp_value(1.0 - latitude + temperature_noise * 0.16 - 0.04, 0.0, 1.0);
	const double aridity = (1.0 - smooth_step(0.30, 0.58, base_moisture)) *
			smooth_step(0.52, 0.84, base_temperature) *
			smooth_step(0.33, 0.78, continent + island * 0.22);
	const double lowland_mask = smooth_step(0.36, 0.72, continent + island * 0.25) *
			(1.0 - smooth_step(0.22, 0.50, mountains));
	const double plains = lowland_mask *
			(1.0 - aridity * 0.62) *
			(1.0 - smooth_step(0.55, 0.82, base_moisture));
	const double wetland = smooth_step(0.66, 0.88, base_moisture + river * 0.20) *
			lowland_mask *
			smooth_step(0.25, 0.68, continent + island * 0.20);
	const double forest = smooth_step(0.54, 0.78, base_moisture) *
			smooth_step(0.24, 0.60, base_temperature) *
			(1.0 - smooth_step(0.44, 0.78, mountains));
	const double hills = smooth_step(0.16, 0.38, mountains) *
			(1.0 - smooth_step(0.48, 0.72, mountains)) *
			smooth_step(0.42, 0.78, continent + island * 0.18);
	const double alpine = smooth_step(0.48, 0.76, mountains) *
			smooth_step(0.52, 0.86, continent + island * 0.12);
	r_alpine = alpine;
	const double dune_detail = ridged(
			(p_x + 541.0) / 360.0,
			(p_z - 877.0) / 360.0,
			p_profile.seed + 353,
			3);

	const double micro = p_include_micro ?
			fbm(
					p_x / 118.0,
					p_z / 118.0,
					p_profile.seed + 71,
					4) :
			0.0;

	const double lowland_flatness = clamp_value(
			std::max(plains * 0.80, std::max(aridity * 0.72, wetland * 0.68)) *
					(1.0 - smooth_step(0.32, 0.64, mountains)),
			0.0,
			1.0);
	const double mountain_factor = lerp_value(0.48, 1.14, clamp_value(alpine + hills * 0.24, 0.0, 1.0));
	const double shelf_factor = lerp_value(0.20, 0.34, 1.0 - lowland_flatness);
	const double detail_factor = p_profile.detail_weight *
			lerp_value(0.42, 1.16, clamp_value(alpine + hills * 0.45, 0.0, 1.0)) *
			lerp_value(1.0, 0.62, lowland_flatness);

	double height =
			((basin - 0.44) * p_profile.height_scale * 0.72) +
			(shelf * broad * p_profile.height_scale * shelf_factor) +
			(mountains * p_profile.height_scale * mountain_factor) +
			(micro * p_profile.height_scale * detail_factor) +
			(island * p_profile.height_scale * 0.36);

	const double lowland_target =
			((basin - 0.46) * p_profile.height_scale * 0.44) +
			((broad - 0.50) * p_profile.height_scale * 0.10) +
			(island * p_profile.height_scale * 0.25);
	height = lerp_value(height, lowland_target, lowland_flatness * 0.62);
	height += aridity * (dune_detail - 0.40) * p_profile.height_scale * 0.075;
	height -= wetland *
			smooth_step(0.26, 0.72, continent + island * 0.20) *
			p_profile.height_scale *
			0.045;

	const double shallow_shelf = shelf * (1.0 - smooth_step(0.14, 0.46, mountains));
	const double waterline_proximity = 1.0 - smooth_step(p_profile.sea_level + 52.0, p_profile.sea_level + 220.0, height);
	height -= shallow_shelf * waterline_proximity * p_profile.height_scale * 0.035;

	const double valley_carve = river * p_profile.river_carve_depth * (0.35 + mountains * 0.85);
	height -= valley_carve * p_profile.valley_weight;

	if (r_terms != nullptr) {
		r_terms->continent = continent;
		r_terms->basin = basin;
		r_terms->shelf = shelf;
		r_terms->mountains = mountains;
		r_terms->broad_elevation = broad;
		r_terms->river = river;
		r_terms->base_moisture = base_moisture;
		r_terms->base_temperature = base_temperature;
		r_terms->aridity = aridity;
		r_terms->plains = plains;
		r_terms->wetland = wetland;
		r_terms->forest = forest;
		r_terms->hills = hills;
		r_terms->alpine = alpine;
		r_terms->island = island;
		r_terms->dune_detail = dune_detail;
	}

	return height;
}

double compute_land_balance_offset(const NativeTerrainProfile &p_profile) {
	constexpr int resolution = 33;
	constexpr double target_land_ratio = 0.58;
	constexpr double correction_strength = 0.48;
	const double extent = std::max(p_profile.chunk_size * 48.0, p_profile.continent_scale * 2.2);
	int land_count = 0;

	for (int y = 0; y < resolution; y++) {
		for (int x = 0; x < resolution; x++) {
			const double tx = static_cast<double>(x) / static_cast<double>(resolution - 1);
			const double ty = static_cast<double>(y) / static_cast<double>(resolution - 1);
			const double world_x = (tx - 0.5) * extent;
			const double world_z = (ty - 0.5) * extent;
			double mountains = 0.0;
			double alpine = 0.0;
			const double height = sample_height_unbalanced(world_x, world_z, p_profile, false, mountains, alpine);
			if (height >= p_profile.sea_level + 3.0) {
				land_count++;
			}
		}
	}

	const double land_ratio = static_cast<double>(land_count) / static_cast<double>(resolution * resolution);
	const double offset = (land_ratio - target_land_ratio) * p_profile.height_scale * correction_strength;
	return clamp_value(offset, p_profile.height_scale * -0.16, p_profile.height_scale * 0.16);
}

double sample_height_native(double p_x, double p_z, const NativeTerrainProfile &p_profile, NativeTerrainTerms *r_terms = nullptr) {
	double mountains = 0.0;
	double alpine = 0.0;
	double height = sample_height_unbalanced(p_x, p_z, p_profile, true, mountains, alpine, r_terms);
	height -= p_profile.land_balance_offset;

	const double terrace_mask = smooth_step(0.52, 0.86, mountains) * p_profile.vista_frequency * lerp_value(0.55, 1.0, alpine);
	return terrace(height, std::max(12.0, p_profile.terrace_strength), terrace_mask * 0.38);
}

double compute_scenic_potential(
		double p_height,
		const NativeTerrainProfile &p_profile,
		const NativeTerrainTerms &p_terms,
		double p_moisture,
		double p_temperature) {
	const double elevation_score = smooth_step(p_profile.sea_level + 48.0, p_profile.sea_level + p_profile.height_scale * 0.46, p_height);
	const double ridge_score = smooth_step(0.10, 0.34, p_terms.mountains);
	const double river_contrast = smooth_step(0.20, 0.58, p_terms.river) *
			smooth_step(p_profile.sea_level + 18.0, p_profile.sea_level + p_profile.height_scale * 0.34, p_height);
	const double highland_score = smooth_step(0.30, 0.62, p_terms.shelf * p_terms.broad_elevation);
	const double biome_contrast = clamp_value(std::abs(p_moisture - p_temperature) * 1.35, 0.0, 1.0);
	const double coast_drama = clamp_value(1.0 - std::abs(p_height - p_profile.sea_level - 22.0) / 180.0, 0.0, 1.0) *
			clamp_value(p_terms.continent * 1.5, 0.0, 1.0);
	const double desert_vista = p_terms.aridity *
			smooth_step(p_profile.sea_level + 34.0, p_profile.sea_level + 260.0, p_height) *
			(1.0 - smooth_step(0.34, 0.64, p_terms.mountains));
	const double island_vista = p_terms.island *
			clamp_value(1.0 - std::abs(p_height - p_profile.sea_level - 58.0) / 260.0, 0.0, 1.0);

	const double dominant_vista = std::max(
			std::max(ridge_score * 0.92, river_contrast * 0.86),
			std::max(std::max(coast_drama * 0.74, highland_score * 0.72), std::max(desert_vista * 0.54, island_vista * 0.64)));

	const double blended_vista =
			ridge_score * 0.30 +
			elevation_score * 0.18 +
			river_contrast * 0.22 +
			highland_score * 0.14 +
			coast_drama * 0.10 +
			biome_contrast * 0.06 +
			desert_vista * 0.06 +
			island_vista * 0.05;

	return clamp_value(std::max(dominant_vista, blended_vista) * (0.94 + p_profile.vista_frequency * 0.12), 0.0, 1.0);
}

double compute_traversability(
		double p_height,
		const NativeTerrainProfile &p_profile,
		const NativeTerrainTerms &p_terms) {
	const double land = smooth_step(p_profile.sea_level + 3.0, p_profile.sea_level + 38.0, p_height);
	const double lowland_bonus = clamp_value(p_terms.plains * 0.18 + p_terms.aridity * 0.10 + p_terms.wetland * 0.04, 0.0, 0.24);
	const double rugged_penalty = clamp_value(p_terms.mountains * 1.45 - lowland_bonus, 0.0, 0.82);
	const double river_penalty = p_terms.river * 0.24;
	return clamp_value(land * (1.0 - rugged_penalty) * (1.0 - river_penalty), 0.0, 1.0);
}

double compute_exposure(
		double p_height,
		const NativeTerrainProfile &p_profile,
		const NativeTerrainTerms &p_terms,
		double p_scenic_potential) {
	const double elevation = smooth_step(p_profile.sea_level + 140.0, p_profile.sea_level + p_profile.height_scale * 0.86, p_height);
	const double ridge = smooth_step(0.20, 0.64, p_terms.mountains);
	const double plateau = smooth_step(0.34, 0.70, p_terms.shelf * p_terms.broad_elevation);
	const double coastal = clamp_value(1.0 - std::abs(p_height - p_profile.sea_level - 18.0) / 210.0, 0.0, 1.0);

	return clamp_value(
			std::max(elevation * 0.58, ridge * 0.70) +
					plateau * 0.16 +
					p_scenic_potential * 0.18 +
					coastal * 0.08,
			0.0,
			1.0);
}

double compute_resource_potential(
		double p_height,
		const NativeTerrainProfile &p_profile,
		const NativeTerrainTerms &p_terms,
		double p_moisture,
		double p_temperature,
		double p_traversability) {
	const double land = smooth_step(p_profile.sea_level + 8.0, p_profile.sea_level + 58.0, p_height);
	const double water_access = smooth_step(0.18, 0.66, p_terms.river);
	const double climate = clamp_value(1.0 - std::abs(p_temperature - 0.54) * 1.75, 0.0, 1.0);
	const double low_elevation = 1.0 - smooth_step(p_profile.sea_level + 320.0, p_profile.sea_level + p_profile.height_scale * 0.92, p_height);
	const double oasis = p_terms.aridity * smooth_step(0.38, 0.78, p_terms.river + p_moisture * 0.24);
	const double arable_lowland = clamp_value(p_terms.plains * 0.12 + p_terms.wetland * 0.16 + oasis * 0.24, 0.0, 0.32);
	const double soil = clamp_value(
			p_moisture * 0.52 +
					climate * 0.22 +
					low_elevation * 0.18 +
					water_access * 0.08 +
					arable_lowland -
					p_terms.aridity * 0.16,
			0.0,
			1.0);

	return clamp_value(land * (soil * 0.72 + p_traversability * 0.28), 0.0, 1.0);
}

double compute_hazard_potential(
		double p_height,
		const NativeTerrainProfile &p_profile,
		const NativeTerrainTerms &p_terms,
		double p_temperature,
		double p_traversability,
		double p_exposure) {
	const double water_depth = clamp_value((p_profile.sea_level - p_height) / std::max(1.0, p_profile.height_scale * 0.38), 0.0, 1.0);
	const double rugged = smooth_step(0.05, 0.32, p_terms.mountains);
	const double canyon = p_terms.river * smooth_step(0.05, 0.30, p_terms.mountains);
	const double river_risk = smooth_step(0.66, 0.92, p_terms.river) *
			smooth_step(p_profile.sea_level + 8.0, p_profile.sea_level + p_profile.height_scale * 0.48, p_height);
	const double high_elevation = smooth_step(p_profile.sea_level + 260.0, p_profile.sea_level + p_profile.height_scale * 0.92, p_height);
	const double exposed_ridge = smooth_step(0.16, 0.52, p_exposure);
	const double snow = p_temperature < 0.22 ?
			smooth_step(p_profile.sea_level + 280.0, p_profile.sea_level + p_profile.height_scale * 0.92, p_height) :
			0.0;
	const double isolation = 1.0 - p_traversability;
	const double heat_risk = p_terms.aridity * smooth_step(0.64, 0.90, p_temperature);
	const double desert_exposure = heat_risk *
			(0.58 + p_terms.dune_detail * 0.42) *
			(1.0 - smooth_step(0.36, 0.66, p_terms.mountains));
	const double flood_risk = p_terms.wetland *
			smooth_step(0.46, 0.86, p_terms.river + p_terms.base_moisture * 0.32) *
			(1.0 - smooth_step(p_profile.sea_level + 180.0, p_profile.sea_level + 420.0, p_height));
	const double island_isolation = p_terms.island *
			(1.0 - smooth_step(0.32, 0.58, p_terms.continent)) *
			smooth_step(p_profile.sea_level + 8.0, p_profile.sea_level + 220.0, p_height);
	const double coastal_storm = clamp_value(1.0 - std::abs(p_height - p_profile.sea_level - 16.0) / 150.0, 0.0, 1.0) *
			smooth_step(0.26, 0.68, p_terms.continent + p_terms.island * 0.28);
	const double frontier_wildland =
			smooth_step(0.42, 0.78, p_terms.plains + p_terms.forest * 0.70 + p_terms.wetland * 0.85 + p_terms.river * 0.22) *
			smooth_step(0.34, 0.72, p_terms.base_moisture + p_terms.river * 0.30) *
			smooth_step(p_profile.sea_level + 12.0, p_profile.sea_level + 380.0, p_height) *
			(1.0 - smooth_step(0.46, 0.76, p_terms.mountains));

	return clamp_value(
			std::max(
					std::max(std::max(rugged * 0.74, canyon * 0.82), river_risk * 0.50),
					std::max(
							std::max(desert_exposure * 0.64, flood_risk * 0.62),
							std::max(coastal_storm * 0.46, frontier_wildland * 0.48))) +
					water_depth * 0.12 +
					high_elevation * 0.16 +
					exposed_ridge * 0.24 +
					snow * 0.08 +
					isolation * 0.16 +
					heat_risk * 0.28 +
					flood_risk * 0.18 +
					island_isolation * 0.20 +
					coastal_storm * 0.10 +
					frontier_wildland * 0.12,
			0.0,
			1.0);
}

double compute_encounter_potential(
		double p_scenic_potential,
		double p_traversability,
		double p_exposure,
		double p_resource_potential,
		double p_hazard_potential) {
	const double risk_reward = std::min(p_resource_potential, p_hazard_potential) * 0.22;
	return clamp_value(
			p_scenic_potential * 0.24 +
					p_traversability * 0.20 +
					p_resource_potential * 0.22 +
					p_hazard_potential * 0.18 +
					p_exposure * 0.16 +
					risk_reward,
			0.0,
			1.0);
}

int classify_biome(
		double p_height,
		const NativeTerrainProfile &p_profile,
		const NativeTerrainTerms &p_terms,
		double p_moisture,
		double p_temperature) {
	if (p_height < p_profile.sea_level - 12.0) {
		return BIOME_OCEAN;
	}

	if (p_height < p_profile.sea_level + 10.0) {
		return BIOME_COAST;
	}

	if (p_height > p_profile.sea_level + 680.0 || (p_temperature < 0.20 && p_height > p_profile.sea_level + 360.0)) {
		return BIOME_SNOWFIELD;
	}

	if (p_terms.mountains > 0.62) {
		return BIOME_MOUNTAINS;
	}

	if (p_terms.aridity > 0.55 &&
			p_terms.river > 0.46 &&
			p_moisture > 0.36 &&
			p_height < p_profile.sea_level + 320.0) {
		return BIOME_OASIS;
	}

	if (p_terms.aridity > 0.48 &&
			p_moisture < 0.56 &&
			p_height < p_profile.sea_level + 460.0) {
		return BIOME_DESERT;
	}

	if (p_terms.island > 0.54 &&
			p_terms.continent < 0.56 &&
			p_height < p_profile.sea_level + 280.0) {
		return BIOME_ISLAND;
	}

	if (p_terms.hills > 0.36 || p_terms.mountains > 0.34) {
		return BIOME_HILLS;
	}

	if (p_terms.wetland > 0.54) {
		return BIOME_WETLAND;
	}

	if (p_terms.forest > 0.48 && p_moisture > 0.56) {
		return BIOME_FOREST;
	}

	if (p_terms.plains > 0.42 && p_height < p_profile.sea_level + 300.0) {
		return BIOME_PLAINS;
	}

	return BIOME_GRASSLAND;
}

int classify_landscape(
		double p_height,
		const NativeTerrainProfile &p_profile,
		const NativeTerrainTerms &p_terms,
		double p_moisture,
		double p_temperature,
		double p_scenic_potential,
		int p_biome) {
	if (p_height < p_profile.sea_level - 12.0) {
		return LANDSCAPE_OCEAN;
	}

	if (p_height < p_profile.sea_level + 12.0) {
		return LANDSCAPE_COAST;
	}

	if (p_height > p_profile.sea_level + 680.0 || (p_temperature < 0.20 && p_height > p_profile.sea_level + 360.0)) {
		return LANDSCAPE_SNOWFIELD;
	}

	if (p_terms.river > 0.68 && p_terms.mountains > 0.34) {
		return LANDSCAPE_CANYON;
	}

	if (p_biome == BIOME_OASIS || p_biome == BIOME_DESERT) {
		return p_terms.hills > 0.42 ? LANDSCAPE_HIGHLANDS : LANDSCAPE_LOWLAND;
	}

	if (p_terms.river > 0.62) {
		return LANDSCAPE_RIVER_VALLEY;
	}

	if (p_terms.mountains > 0.62) {
		return LANDSCAPE_MOUNTAIN_MASSIF;
	}

	if (p_scenic_potential > 0.68 && p_height > p_profile.sea_level + 180.0) {
		return LANDSCAPE_VISTA_PLATEAU;
	}

	if (p_height > p_profile.sea_level + 360.0 || p_terms.mountains > 0.36) {
		return LANDSCAPE_HIGHLANDS;
	}

	if (p_moisture > 0.76 && p_temperature > 0.34 && p_height < p_profile.sea_level + 260.0) {
		return LANDSCAPE_WETLAND;
	}

	if (p_moisture > 0.62 && p_temperature > 0.28) {
		return LANDSCAPE_FOREST_BASIN;
	}

	return LANDSCAPE_LOWLAND;
}

NativeTerrainField build_field_native(double p_height, const NativeTerrainProfile &p_profile, const NativeTerrainTerms &p_terms) {
	NativeTerrainField field;
	field.height = p_height;
	field.continent = p_terms.continent;
	field.basin = p_terms.basin;
	field.shelf = p_terms.shelf;
	field.mountains = p_terms.mountains;
	field.broad_elevation = p_terms.broad_elevation;
	field.river = p_terms.river;
	field.moisture = clamp_value(
			p_terms.base_moisture + p_terms.river * 0.45 - p_terms.aridity * 0.22 + p_terms.wetland * 0.16,
			0.0,
			1.0);
	field.temperature = clamp_value(
			p_terms.base_temperature -
					std::max(0.0, p_height) / (p_profile.height_scale * 1.7) -
					p_terms.alpine * 0.08,
			0.0,
			1.0);
	field.scenic_potential = compute_scenic_potential(p_height, p_profile, p_terms, field.moisture, field.temperature);
	field.traversability = compute_traversability(p_height, p_profile, p_terms);
	field.exposure = compute_exposure(p_height, p_profile, p_terms, field.scenic_potential);
	field.resource_potential = compute_resource_potential(p_height, p_profile, p_terms, field.moisture, field.temperature, field.traversability);
	field.hazard_potential = compute_hazard_potential(p_height, p_profile, p_terms, field.temperature, field.traversability, field.exposure);
	field.encounter_potential = compute_encounter_potential(
			field.scenic_potential,
			field.traversability,
			field.exposure,
			field.resource_potential,
			field.hazard_potential);
	field.biome_kind = classify_biome(p_height, p_profile, p_terms, field.moisture, field.temperature);
	field.landscape_kind = classify_landscape(
			p_height,
			p_profile,
			p_terms,
			field.moisture,
			field.temperature,
			field.scenic_potential,
			field.biome_kind);
	return field;
}

NativeTerrainProfile make_profile_base(
		int32_t p_seed,
		double p_chunk_size,
		double p_height_scale,
		double p_sea_level,
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
	profile.chunk_size = std::max(16.0, p_chunk_size);
	profile.height_scale = p_height_scale;
	profile.sea_level = p_sea_level;
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

NativeTerrainProfile make_profile(
		int32_t p_seed,
		double p_chunk_size,
		double p_height_scale,
		double p_sea_level,
		double p_continent_scale,
		double p_mountain_scale,
		double p_mountain_weight,
		double p_valley_weight,
		double p_detail_weight,
		double p_vista_frequency,
		double p_river_strength,
		double p_river_carve_depth,
		double p_terrace_strength) {
	NativeTerrainProfile profile = make_profile_base(
			p_seed,
			p_chunk_size,
			p_height_scale,
			p_sea_level,
			p_continent_scale,
			p_mountain_scale,
			p_mountain_weight,
			p_valley_weight,
			p_detail_weight,
			p_vista_frequency,
			p_river_strength,
			p_river_carve_depth,
			p_terrace_strength);
	profile.land_balance_offset = compute_land_balance_offset(profile);
	return profile;
}

NativeTerrainProfile make_profile_with_land_balance(
		int32_t p_seed,
		double p_chunk_size,
		double p_height_scale,
		double p_sea_level,
		double p_continent_scale,
		double p_mountain_scale,
		double p_mountain_weight,
		double p_valley_weight,
		double p_detail_weight,
		double p_vista_frequency,
		double p_river_strength,
		double p_river_carve_depth,
		double p_terrace_strength,
		double p_land_balance_offset) {
	NativeTerrainProfile profile = make_profile_base(
			p_seed,
			p_chunk_size,
			p_height_scale,
			p_sea_level,
			p_continent_scale,
			p_mountain_scale,
			p_mountain_weight,
			p_valley_weight,
			p_detail_weight,
			p_vista_frequency,
			p_river_strength,
			p_river_carve_depth,
			p_terrace_strength);
	profile.land_balance_offset = p_land_balance_offset;
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
					"chunk_size",
					"height_scale",
					"sea_level",
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
					"sea_level",
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
		double p_chunk_size,
		double p_height_scale,
		double p_sea_level,
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
			p_chunk_size,
			p_height_scale,
			p_sea_level,
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
		double p_sea_level,
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
			p_chunk_size,
			p_height_scale,
			p_sea_level,
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

int write_height_grid_native(
		double p_origin_x,
		double p_origin_z,
		int32_t p_resolution,
		double p_chunk_size,
		const NativeTerrainProfile &p_profile,
		float *r_output_heights,
		int32_t p_output_height_count) {
	const int resolution = std::max(1, std::min(512, p_resolution));
	const int width = resolution + 1;
	const int required_count = width * width;

	if (r_output_heights == nullptr || p_output_height_count < required_count) {
		return -required_count;
	}

	const double step = p_chunk_size / static_cast<double>(resolution);

	for (int z = 0; z < width; z++) {
		for (int x = 0; x < width; x++) {
			const int index = z * width + x;
			const double world_x = p_origin_x + static_cast<double>(x) * step;
			const double world_z = p_origin_z + static_cast<double>(z) * step;
			r_output_heights[index] = static_cast<float>(sample_height_native(world_x, world_z, p_profile));
		}
	}

	return required_count;
}

int write_field_grid_native(
		double p_origin_x,
		double p_origin_z,
		int32_t p_resolution,
		double p_chunk_size,
		const NativeTerrainProfile &p_profile,
		float *r_output_samples,
		int32_t p_output_sample_float_count) {
	constexpr int stride = 17;
	const int resolution = std::max(1, std::min(512, p_resolution));
	const int width = resolution + 1;
	const int required_count = width * width * stride;

	if (r_output_samples == nullptr || p_output_sample_float_count < required_count) {
		return -required_count;
	}

	const double step = p_chunk_size / static_cast<double>(resolution);

	for (int z = 0; z < width; z++) {
		for (int x = 0; x < width; x++) {
			const int sample_index = z * width + x;
			const int offset = sample_index * stride;
			const double world_x = p_origin_x + static_cast<double>(x) * step;
			const double world_z = p_origin_z + static_cast<double>(z) * step;
			NativeTerrainTerms terms;
			const double height = sample_height_native(world_x, world_z, p_profile, &terms);

			r_output_samples[offset] = static_cast<float>(height);
			r_output_samples[offset + 1] = static_cast<float>(terms.continent);
			r_output_samples[offset + 2] = static_cast<float>(terms.basin);
			r_output_samples[offset + 3] = static_cast<float>(terms.shelf);
			r_output_samples[offset + 4] = static_cast<float>(terms.mountains);
			r_output_samples[offset + 5] = static_cast<float>(terms.broad_elevation);
			r_output_samples[offset + 6] = static_cast<float>(terms.river);
			r_output_samples[offset + 7] = static_cast<float>(terms.base_moisture);
			r_output_samples[offset + 8] = static_cast<float>(terms.base_temperature);
			r_output_samples[offset + 9] = static_cast<float>(terms.aridity);
			r_output_samples[offset + 10] = static_cast<float>(terms.plains);
			r_output_samples[offset + 11] = static_cast<float>(terms.wetland);
			r_output_samples[offset + 12] = static_cast<float>(terms.forest);
			r_output_samples[offset + 13] = static_cast<float>(terms.hills);
			r_output_samples[offset + 14] = static_cast<float>(terms.alpine);
			r_output_samples[offset + 15] = static_cast<float>(terms.island);
			r_output_samples[offset + 16] = static_cast<float>(terms.dune_detail);
		}
	}

	return required_count;
}

int write_derived_field_grid_native(
		double p_origin_x,
		double p_origin_z,
		int32_t p_resolution,
		double p_chunk_size,
		const NativeTerrainProfile &p_profile,
		float *r_output_samples,
		int32_t p_output_sample_float_count) {
	constexpr int stride = 17;
	const int resolution = std::max(1, std::min(512, p_resolution));
	const int width = resolution + 1;
	const int required_count = width * width * stride;

	if (r_output_samples == nullptr || p_output_sample_float_count < required_count) {
		return -required_count;
	}

	const double step = p_chunk_size / static_cast<double>(resolution);

	for (int z = 0; z < width; z++) {
		for (int x = 0; x < width; x++) {
			const int sample_index = z * width + x;
			const int offset = sample_index * stride;
			const double world_x = p_origin_x + static_cast<double>(x) * step;
			const double world_z = p_origin_z + static_cast<double>(z) * step;
			NativeTerrainTerms terms;
			const double height = sample_height_native(world_x, world_z, p_profile, &terms);
			const NativeTerrainField field = build_field_native(height, p_profile, terms);

			r_output_samples[offset] = static_cast<float>(field.height);
			r_output_samples[offset + 1] = static_cast<float>(field.continent);
			r_output_samples[offset + 2] = static_cast<float>(field.basin);
			r_output_samples[offset + 3] = static_cast<float>(field.shelf);
			r_output_samples[offset + 4] = static_cast<float>(field.mountains);
			r_output_samples[offset + 5] = static_cast<float>(field.broad_elevation);
			r_output_samples[offset + 6] = static_cast<float>(field.river);
			r_output_samples[offset + 7] = static_cast<float>(field.moisture);
			r_output_samples[offset + 8] = static_cast<float>(field.temperature);
			r_output_samples[offset + 9] = static_cast<float>(field.scenic_potential);
			r_output_samples[offset + 10] = static_cast<float>(field.traversability);
			r_output_samples[offset + 11] = static_cast<float>(field.exposure);
			r_output_samples[offset + 12] = static_cast<float>(field.resource_potential);
			r_output_samples[offset + 13] = static_cast<float>(field.hazard_potential);
			r_output_samples[offset + 14] = static_cast<float>(field.encounter_potential);
			r_output_samples[offset + 15] = static_cast<float>(field.biome_kind);
			r_output_samples[offset + 16] = static_cast<float>(field.landscape_kind);
		}
	}

	return required_count;
}

extern "C" GDE_EXPORT int dao_native_sample_height_grid(
		int32_t p_seed,
		double p_origin_x,
		double p_origin_z,
		int32_t p_resolution,
		double p_chunk_size,
		double p_height_scale,
		double p_sea_level,
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
	const NativeTerrainProfile profile = make_profile(
			p_seed,
			p_chunk_size,
			p_height_scale,
			p_sea_level,
			p_continent_scale,
			p_mountain_scale,
			p_mountain_weight,
			p_valley_weight,
			p_detail_weight,
			p_vista_frequency,
			p_river_strength,
			p_river_carve_depth,
			p_terrace_strength);

	return write_height_grid_native(
			p_origin_x,
			p_origin_z,
			p_resolution,
			p_chunk_size,
			profile,
			r_output_heights,
			p_output_height_count);
}

extern "C" GDE_EXPORT int dao_native_sample_field_grid_v1(
		int32_t p_seed,
		double p_origin_x,
		double p_origin_z,
		int32_t p_resolution,
		double p_chunk_size,
		double p_height_scale,
		double p_sea_level,
		double p_continent_scale,
		double p_mountain_scale,
		double p_mountain_weight,
		double p_valley_weight,
		double p_detail_weight,
		double p_vista_frequency,
		double p_river_strength,
		double p_river_carve_depth,
		double p_terrace_strength,
		double p_land_balance_offset,
		float *r_output_samples,
		int32_t p_output_sample_float_count) {
	const NativeTerrainProfile profile = make_profile_with_land_balance(
			p_seed,
			p_chunk_size,
			p_height_scale,
			p_sea_level,
			p_continent_scale,
			p_mountain_scale,
			p_mountain_weight,
			p_valley_weight,
			p_detail_weight,
			p_vista_frequency,
			p_river_strength,
			p_river_carve_depth,
			p_terrace_strength,
			p_land_balance_offset);

	return write_field_grid_native(
			p_origin_x,
			p_origin_z,
			p_resolution,
			p_chunk_size,
			profile,
			r_output_samples,
			p_output_sample_float_count);
}

extern "C" GDE_EXPORT int dao_native_sample_field_grid_v2(
		int32_t p_seed,
		double p_origin_x,
		double p_origin_z,
		int32_t p_resolution,
		double p_chunk_size,
		double p_height_scale,
		double p_sea_level,
		double p_continent_scale,
		double p_mountain_scale,
		double p_mountain_weight,
		double p_valley_weight,
		double p_detail_weight,
		double p_vista_frequency,
		double p_river_strength,
		double p_river_carve_depth,
		double p_terrace_strength,
		double p_land_balance_offset,
		float *r_output_samples,
		int32_t p_output_sample_float_count) {
	const NativeTerrainProfile profile = make_profile_with_land_balance(
			p_seed,
			p_chunk_size,
			p_height_scale,
			p_sea_level,
			p_continent_scale,
			p_mountain_scale,
			p_mountain_weight,
			p_valley_weight,
			p_detail_weight,
			p_vista_frequency,
			p_river_strength,
			p_river_carve_depth,
			p_terrace_strength,
			p_land_balance_offset);

	return write_derived_field_grid_native(
			p_origin_x,
			p_origin_z,
			p_resolution,
			p_chunk_size,
			profile,
			r_output_samples,
			p_output_sample_float_count);
}

extern "C" GDE_EXPORT int dao_native_sample_height_grid_v2(
		int32_t p_seed,
		double p_origin_x,
		double p_origin_z,
		int32_t p_resolution,
		double p_chunk_size,
		double p_height_scale,
		double p_sea_level,
		double p_continent_scale,
		double p_mountain_scale,
		double p_mountain_weight,
		double p_valley_weight,
		double p_detail_weight,
		double p_vista_frequency,
		double p_river_strength,
		double p_river_carve_depth,
		double p_terrace_strength,
		double p_land_balance_offset,
		float *r_output_heights,
		int32_t p_output_height_count) {
	const NativeTerrainProfile profile = make_profile_with_land_balance(
			p_seed,
			p_chunk_size,
			p_height_scale,
			p_sea_level,
			p_continent_scale,
			p_mountain_scale,
			p_mountain_weight,
			p_valley_weight,
			p_detail_weight,
			p_vista_frequency,
			p_river_strength,
			p_river_carve_depth,
			p_terrace_strength,
			p_land_balance_offset);

	return write_height_grid_native(
			p_origin_x,
			p_origin_z,
			p_resolution,
			p_chunk_size,
			profile,
			r_output_heights,
			p_output_height_count);
}

} // namespace godot
