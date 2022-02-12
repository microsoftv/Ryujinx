using Ryujinx.Graphics.Shader;
using System.Linq;

namespace Ryujinx.Graphics.GAL.Multithreading.Resources.Programs
{
    class SourceProgramRequest : IProgramRequest
    {
        public ThreadedProgram Threaded { get; set; }

        private IShader[] _shaders;
        private ShaderStage _stage;
        private string _code;

        public SourceProgramRequest(ThreadedProgram program, IShader[] shaders)
        {
            Threaded = program;

            _shaders = shaders;
        }

        public SourceProgramRequest(ThreadedProgram program, ShaderStage stage, string code)
        {
            Threaded = program;

            _stage = stage;
            _code = code;
        }

        public IProgram Create(IRenderer renderer)
        {
            if (_shaders != null)
            {
                IShader[] shaders = _shaders.Select(shader =>
                {
                    var threaded = (ThreadedShader)shader;
                    threaded?.EnsureCreated();
                    return threaded?.Base;
                }).ToArray();

                return renderer.CreateProgram(shaders);
            }
            else
            {
                return renderer.CreateProgramSeparate(_stage, _code);
            }
        }
    }
}
