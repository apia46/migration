shader_type canvas_item;

uniform vec2 ScreenSize;
uniform vec2 CameraPosition;
uniform float CameraZoom;

void fragment() {
	vec2 worldScreenSize = ScreenSize/CameraZoom;
	COLOR.rgb = texture(TEXTURE, (floor(UV*worldScreenSize) + vec2(0.5,0.5))/worldScreenSize).rgb;
}
