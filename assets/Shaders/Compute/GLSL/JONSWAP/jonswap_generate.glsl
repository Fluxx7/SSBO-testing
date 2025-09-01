#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(rgba16f, set = 0, binding = 0) restrict writeonly uniform image2D GeneratedWaveBuffer;

layout(push_constant) restrict readonly uniform PushConstants {
    uint waveCount;
    
};

