#pragma once

#include <godot_cpp/classes/object.hpp>
#include <godot_cpp/variant/string.hpp>

namespace godot {

class DaoExtension : public Object {
	GDCLASS(DaoExtension, Object);

protected:
	static void _bind_methods();

public:
	static String health_check();
};

} // namespace godot
