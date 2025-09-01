#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0) restrict readonly uniform image2D pre_transpose;

layout(set = 0, binding = 1) restrict writeonly uniform image2D post_transpose;


void main() {

}