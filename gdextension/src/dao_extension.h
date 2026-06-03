#pragma once

#include <godot_cpp/classes/object.hpp>
#include <godot_cpp/variant/packed_float32_array.hpp>
#include <godot_cpp/variant/string.hpp>

namespace godot {

class DaoExtension : public Object {
	GDCLASS(DaoExtension, Object);

protected:
	static void _bind_methods();

public:
	static String health_check();
	static double sample_height(
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
			double p_terrace_strength);
	static PackedFloat32Array sample_height_grid(
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
			double p_terrace_strength);
};

} // namespace godot
