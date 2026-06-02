#include "dao_extension.h"

#include <godot_cpp/core/class_db.hpp>

namespace godot {

void DaoExtension::_bind_methods() {
	ClassDB::bind_static_method("DaoExtension", D_METHOD("health_check"), &DaoExtension::health_check);
}

String DaoExtension::health_check() {
	return "dao gdextension loaded";
}

} // namespace godot
