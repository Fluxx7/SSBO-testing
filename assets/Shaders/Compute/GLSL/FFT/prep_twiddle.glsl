#[compute]
#version 450

#define PI 3.1415926535897932384626433

layout(local_size_x = 16, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) buffer restrict twiddleFactors {
    vec2 twiddles[];
};

layout(push_constant) restrict readonly uniform PushConstants {
    int N;
};

void main() {
    if (gl_GlobalInvocationID.x >= (N / 2)) return;
    uint x = gl_GlobalInvocationID.x;
    float theta = 2.0 * PI * float(x) / float(N);
    twiddles[x] = vec2(cos(theta), sin(theta));
}