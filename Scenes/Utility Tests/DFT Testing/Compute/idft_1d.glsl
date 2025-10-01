#[compute]
#version 450

#define PI 3.1415926535897932384626433

layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) restrict readonly buffer frequencyBuffer {
    vec2 freqDomain[];
};

layout(set = 0, binding = 1, std430) restrict writeonly buffer timeBuffer {
    vec2 timeDomain[];
};

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
    int n = int(gl_GlobalInvocationID.x);
    vec2 sum = vec2(0.0);

    for (uint k = 0; k < N; k++) {
        sum += complex_mult( freqDomain[k], exp_j( 2.0 * PI * float(k * n)/float(N) ) );
    }

    timeDomain[n] = sum / float(N);
}