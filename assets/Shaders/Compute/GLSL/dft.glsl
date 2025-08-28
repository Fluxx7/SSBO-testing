#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

// 256x256 texture of wave values
// red is real amplitude, green is complex amplitude, blue is phase
// direction from center is the direction of the wave, distance is frequency
layout(rgba16f, set = 0, binding = 0) restrict uniform image2Darray GeneratedWaveBuffer;

layout(rgba16f, set = 0, binding = 1) writeonly uniform sampler2D GeneratedWaveDisplacements;
layout(rgba16f, set = 0, binding = 2) writeonly uniform sampler2D GeneratedWaveNormals;

layout(push_constant) restrict readonly uniform PushConstants {
    uint waveCount;
};

layout(rgba16f, set = 1, binding = 0) restrict image2D displacementBuffer;

void main() {
    uvec2 coord = gl_GlobalInvocationID.xy;
}