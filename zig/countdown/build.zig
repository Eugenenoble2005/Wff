const std = @import("std");
const Scanner = @import("wayland").Scanner;

pub fn build(b: *std.Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});
    const exe = b.addExecutable(.{ .name = "countdown", .target = target, .optimize = optimize, .root_source_file = b.path("main.zig") });
    const run_step = b.step("run", "Run countown");
    const run_exe = b.addRunArtifact(exe);
    run_step.dependOn(&run_exe.step);

    //wayland stuff
    const scanner = Scanner.create(b, .{});
    const wayland = b.createModule(.{ .root_source_file = scanner.result });
    scanner.addCustomProtocol(b.path("layer-shell.xml"));
    exe.root_module.addImport("wayland", wayland);
    scanner.addSystemProtocol("stable/xdg-shell/xdg-shell.xml");
    scanner.generate("wl_compositor", 4);
    scanner.generate("wl_shm", 1);
    scanner.generate("zwlr_layer_shell_v1", 1);
    scanner.generate("wl_output", 4);
    exe.linkSystemLibrary("wayland-client");
    const cairo = b.addTranslateC(.{ .root_source_file = b.addWriteFiles().add("./cairo.h",
        \\ #include <cairo/cairo.h>
    ), .optimize = optimize, .target = target, .link_libc = true });
    exe.root_module.addImport("cairo", cairo.createModule());
    exe.linkSystemLibrary("cairo");
    exe.linkLibC();
    b.installArtifact(exe);
}
