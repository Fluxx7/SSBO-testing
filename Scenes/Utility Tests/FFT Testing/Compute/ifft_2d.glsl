#[compute]
#version 450

#define PI 3.1415926535897932384626433

layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

layout(rgba16f, set = 0, binding = 0) restrict readonly uniform image2D frequencyTexture;

layout(rgba16f, set = 0, binding = 1) restrict writeonly uniform image2D timeTexture;

layout(push_constant) restrict readonly uniform PushConstants {
    uint N;
};

// (ax + j*ay) * (bx + j*by) 
// = ax*bx - ay*by + j(ay*bx + ax*by)
vec2 complex_mult(vec2 a, vec2 b) {
    return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

vec2 exp_j(float theta) {
    return vec2(cos(theta), sin(theta));
}

void main() {
    if (gl_GlobalInvocationID.x >= N) return;
    if (gl_GlobalInvocationID.y >= N) return;
    ivec2 nm = ivec2(gl_GlobalInvocationID.xy);
    vec2 sum = vec2(0.0);

    for (uint k1 = 0; k1 < N; k1++) {
        for (uint k2 = 0; k2 < N; k2++) {
            sum += complex_mult( imageLoad(frequencyTexture, ivec2(k1, k2)).xy, exp_j( 2.0 * PI * float(k1 * nm.x + k2 * nm.y)/float(N * N) ) );
        }
    }
    
    imageStore(timeTexture, nm, vec4(sum/(N * N), 0.0, 0.0));
}