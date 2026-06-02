#pragma once

#include <godot_cpp/core/class_db.hpp>

namespace godot {

void initialize_dao_module(ModuleInitializationLevel p_level);
void uninitialize_dao_module(ModuleInitializationLevel p_level);

} // namespace godot
