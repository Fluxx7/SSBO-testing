shader_type canvas_item;

uniform sampler2DArray debug_tex;
uniform int debug_tex_index;


void fragment() {
	COLOR = texture(debug_tex, vec3(UV, float(debug_tex_index)));
}
