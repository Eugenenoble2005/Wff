Extremely simple GUI frontend for wf-recorder, Screen recorder for wlroots compositors

# Dependencies
Zig(for build), dotnet 9, slurp, wf-recorder, wlr-randr, ffmpeg, wayland, wayland-protocols
You do not need dotnet-runtime installed after compiling, Wff compiles to a native linux binary.

# Install
You can install this from the AUR

```
yay -S wff-git
```

# Local Build
cd into Wff/Wff.Desktop and run
```
dotnet publish -o dist && rm dist/Wff.Desktop.dbg
```
The executable will be in the dist directory
