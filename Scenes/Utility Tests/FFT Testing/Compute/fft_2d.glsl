#[compute]
#version 450

#define PI 3.1415926535897932384626433

layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

layout(rgba16f, set = 0, binding = 0) restrict readonly uniform image2D sampleTexture;

layout(rgba16f, set = 0, binding = 1) restrict writeonly uniform image2D frequencyTexture;

layout(push_constant) restrict readonly uniform PushConstants {
    uint N;
};

// (ax + j*ay) * (bx + j*by) 
// = ax*bx - ay*by + j(ay*bx + ax*by)
vec2 complex_mult(vec2 a, vec2 b) {
    return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

// (ax + j*0) * (bx + j*by) 
// = ax*bx - 0*by + j(0*bx + ax*by)
vec2 complex_mult(float a, vec2 b) {
    return vec2(a * b.x, a * b.y );
}

vec2 exp_j(float theta) {
    return vec2(cos(theta), sin(theta));
}

void main() {
    if (gl_GlobalInvocationID.x >= N) return;
    if (gl_GlobalInvocationID.y >= N) return;
    ivec2 k = ivec2(gl_GlobalInvocationID.xy);
    vec2 sum = vec2(0.0);

    for (int n = 0; n < N; n++) {
        for (int m = 0; m < N; m++) {
            sum += complex_mult( imageLoad(sampleTexture, ivec2(n,m)).xy, exp_j( -2.0 * PI * float(k.x * n + k.y * m)/float(N * N) ) );
        }
    }

    imageStore(frequencyTexture, k, vec4(sum, 0.0, 0.0));
}