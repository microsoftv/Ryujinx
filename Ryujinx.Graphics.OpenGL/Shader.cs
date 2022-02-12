using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;

namespace Ryujinx.Graphics.OpenGL
{
    class Shader : IShader
    {
        public int Handle { get; private set; }

        public Shader(ShaderStage stage, string code)
        {
            Handle = GL.CreateShader(stage.Convert());

            GL.ShaderSource(Handle, code);
            GL.CompileShader(Handle);
        }

        public void Dispose()
        {
            if (Handle != 0)
            {
                GL.DeleteShader(Handle);

                Handle = 0;
            }
        }
    }
}
