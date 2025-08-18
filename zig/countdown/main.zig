const std = @import("std");
const posix = std.posix;
const wayland = @import("wayland");
const zwlr = wayland.client.zwlr;
const cairo = @import("cairo");
const wl = wayland.client.wl;
const gpa = std.heap.page_allocator;
const State = @This();
const CairoSurface = cairo.cairo_surface_t;
display: *wl.Display,
wlCompositor: ?*wl.Compositor = null,
wlShm: ?*wl.Shm = null,
layerShell: ?*zwlr.LayerShellV1 = null,
output: ?*Output = null,
targetName: []const u8,
duration: u16,
fn connect() !void {
    //we can expect the GUI to always supply correct args
    const argv = std.os.argv;
    const output_name = std.mem.span(argv[1]);
    const duration = try std.fmt.parseInt(u16, std.mem.span(argv[2]), 10);
    var state: State = .{
        .display = wl.Display.connect(null) catch die("Could not connect to wayland compositor"),
        .duration = duration,
        .targetName = output_name,
    };
    const registy = try state.display.getRegistry();
    registy.setListener(*State, registryListener, &state);

    if (state.display.roundtrip() != .SUCCESS) die("Roundtrip failed");
    while (state.display.dispatch() == .SUCCESS) {}
}

fn die(comptime msg: []const u8) noreturn {
    std.log.err(msg, .{});
    std.posix.exit(1);
}

fn registryListener(registry: *wl.Registry, event: wl.Registry.Event, state: *State) void {
    state.registryEvent(registry, event) catch die("Error in registry");
}

fn registryEvent(state: *State, registry: *wl.Registry, event: wl.Registry.Event) !void {
    switch (event) {
        .global => |ev| {
            if (std.mem.orderZ(u8, ev.interface, wl.Compositor.interface.name) == .eq) {
                state.wlCompositor = try registry.bind(ev.name, wl.Compositor, 4);
            }
            if (std.mem.orderZ(u8, ev.interface, wl.Shm.interface.name) == .eq) {
                state.wlShm = try registry.bind(ev.name, wl.Shm, 1);
            }
            if (std.mem.orderZ(u8, ev.interface, zwlr.LayerShellV1.interface.name) == .eq) {
                state.layerShell = try registry.bind(ev.name, zwlr.LayerShellV1, 1);
            }
            if (std.mem.orderZ(u8, ev.interface, wl.Output.interface.name) == .eq) {
                const wlOutput = try registry.bind(ev.name, wl.Output, 4);
                var output = try gpa.create(Output);
                output.* = .{
                    .wlOutput = wlOutput,
                    .state = state,
                };
                output.setListener();
            }
        },
        .global_remove => |_| {},
    }
}

pub fn main() void {
    connect() catch {};
}

const Output = struct {
    height: u32 = 0,
    width: u32 = 0,
    wlOutput: *wl.Output,
    state: *State,
    layerSurface: ?*zwlr.LayerSurfaceV1 = null,
    wlSurface: ?*wl.Surface = null,
    poolBuffer: ?*PoolBuffer = null,
    selected: bool = false, //whether this output has been selected as the target output
    pub fn setListener(self: *Output) void {
        self.wlOutput.setListener(*Output, outputListener, self);
    }

    fn outputListener(_: *wl.Output, event: wl.Output.Event, output: *Output) void {
        switch (event) {
            .name => |m| {
                if (std.mem.orderZ(u8, m.name, @ptrCast(output.state.targetName)) == .eq) {
                    //for now we need only match the first output
                    output.state.output = output;
                    output.selected = true;
                    std.log.debug("binding output {s}", .{m.name});
                }
            },
            .description => {},
            .done => {
                if (output.selected == false) {
                    //if this output was not used, there's no need keeping it around
                    gpa.destroy(output);
                    return;
                }
                if (output.layerSurface) |_| return;
                //create a layer surface
                output.createLayerSurface() catch die("FATAL");
            },
            .geometry => {},
            .mode => {},
            .scale => {
                //should we really care about scaling here?
            },
        }
    }
    fn createLayerSurface(output: *Output) !void {
        const state = output.state;
        const compositor = state.wlCompositor.?;
        const layerShell = state.layerShell.?;
        const surface = try compositor.createSurface();
        const input_region = try compositor.createRegion();
        defer input_region.destroy();
        surface.setInputRegion(input_region);
        const layerSurface = try layerShell.getLayerSurface(surface, output.wlOutput, .overlay, "countdown");
        layerSurface.setSize(300, 300);
        layerSurface.setAnchor(.{ .top = true, .right = true });
        layerSurface.setExclusiveZone(-1);
        layerSurface.setListener(*Output, layerSurfaceListener, output);
        output.wlSurface = surface;
        output.layerSurface = layerSurface;
        surface.commit();
    }

    fn showCountdown(output: *Output, number: u16) !void {
        const text = try std.fmt.allocPrint(gpa, "{d}", .{number});
        defer gpa.free(text);
        const poolBuffer = output.poolBuffer.?;
        const cr = cairo.cairo_create(poolBuffer.cairoSurface);
        defer cairo.cairo_destroy(cr);
        cairo.cairo_set_operator(cr, cairo.CAIRO_OPERATOR_CLEAR);
        cairo.cairo_paint(cr);
        cairo.cairo_set_operator(cr, cairo.CAIRO_OPERATOR_OVER);
        cairo.cairo_set_source_rgba(cr, 1.0, 0.0, 0.0, 1.0);
        cairo.cairo_select_font_face(cr, "Arial Black", cairo.CAIRO_FONT_SLANT_ITALIC, cairo.CAIRO_FONT_WEIGHT_BOLD);
        cairo.cairo_set_font_size(cr, 70.0);
        const x_coord = output.width - 100;
        cairo.cairo_move_to(cr, @as(f64, @floatFromInt(x_coord)), 50.0);
        cairo.cairo_show_text(cr, @ptrCast(text));
        output.wlSurface.?.attach(poolBuffer.wlBuffer, 0, 0);
        output.wlSurface.?.damageBuffer(0, 0, @intCast(output.width), @intCast(output.height));
        output.wlSurface.?.commit();
        _ = output.state.display.flush();
        
        //crude recursion based timer, maybe timer_fd here? I'm too lazy
        std.Thread.sleep(1 * std.time.ns_per_s);
        if (number > 1) {
            _ = output.state.display.flush();
            try output.showCountdown(number - 1);
        } else {
            //exit gracefully, we've done our job
            std.posix.exit(0);
        }
    }
    fn layerSurfaceListener(_: *zwlr.LayerSurfaceV1, event: zwlr.LayerSurfaceV1.Event, output: *Output) void {
        switch (event) {
            .configure => |c| {
                output.width = c.width;
                output.height = c.height;
                output.layerSurface.?.ackConfigure(c.serial);
                output.poolBuffer = PoolBuffer.new(output) catch return;
                defer output.poolBuffer.?.deinit();
                output.showCountdown(output.state.duration) catch return;
            },
            .closed => {},
        }
    }
};

const PoolBuffer = struct {
    wlBuffer: *wl.Buffer,
    memoryMap: []align(4096) u8,
    cairoSurface: *CairoSurface,
    pub fn new(output: *Output) !*PoolBuffer {
        const stride: u32 = output.width * 4;
        const fd = try posix.memfd_create("buffer", 0);
        defer posix.close(fd);
        const size = output.height * stride;
        try posix.ftruncate(fd, @intCast(size));
        const data = try posix.mmap(
            null,
            @intCast(size),
            posix.PROT.READ | posix.PROT.WRITE,
            .{ .TYPE = .SHARED },
            fd,
            0,
        );
        const shmPool = try output.state.wlShm.?.createPool(fd, @intCast(size));
        defer shmPool.destroy();
        const wlBuffer = try shmPool.createBuffer(
            0,
            @intCast(output.width),
            @intCast(output.height),
            @intCast(stride),
            .argb8888,
        );
        const cairoSurface = cairo.cairo_image_surface_create_for_data(
            @ptrCast(@alignCast(data)),
            cairo.CAIRO_FORMAT_ARGB32,
            @intCast(output.width),
            @intCast(output.height),
            @intCast(stride),
        ).?;
        const poolBuffer = try gpa.create(PoolBuffer);
        poolBuffer.* = .{
            .wlBuffer = wlBuffer,
            .memoryMap = data,
            .cairoSurface = cairoSurface,
        };
        return poolBuffer;
    }
    pub fn deinit(buffer: *PoolBuffer) void {
        std.posix.munmap(buffer.memoryMap);
        gpa.destroy(buffer);
    }
};
