# GDExtension

This project uses `godot-cpp` as a Git submodule and targets Godot 4.6 explicitly.

## Update

```powershell
git submodule update --init --recursive
git -C gdextension/godot-cpp fetch origin
git -C gdextension/godot-cpp checkout master
git -C gdextension/godot-cpp pull --ff-only
```

After updating `godot-cpp`, rebuild the extension and commit the new submodule SHA.

## Build

From `gdextension/`:

```powershell
scons target=template_debug platform=windows arch=x86_64 api_version=4.6
scons target=template_release platform=windows arch=x86_64 api_version=4.6
```

On Windows, run from a Visual Studio Developer PowerShell for MSVC builds. If MSVC
is unavailable, `godot-cpp` falls back to MinGW when it is installed.

The generated libraries are written into `dao/bin/` and are loaded by
`dao/bin/dao.gdextension`. Intermediate object files for this extension are
written into `gdextension/build/`.

## Smoke Test

After opening the Godot project, this GDScript expression should return
`dao gdextension loaded`:

```gdscript
DaoExtension.health_check()
```
