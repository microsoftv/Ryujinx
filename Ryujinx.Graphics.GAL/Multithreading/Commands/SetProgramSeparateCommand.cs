using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;
using Ryujinx.Graphics.Shader;

namespace Ryujinx.Graphics.GAL.Multithreading.Commands
{
    struct SetProgramSeparateCommand : IGALCommand
    {
        public CommandType CommandType => CommandType.SetProgramSeparate;
        private ShaderStage _stage;
        private TableRef<IProgram> _program;

        public void Set(ShaderStage stage, TableRef<IProgram> program)
        {
            _stage = stage;
            _program = program;
        }

        public static void Run(ref SetProgramSeparateCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            ThreadedProgram program = command._program.GetAs<ThreadedProgram>(threaded);

            if (program != null)
            {
                threaded.Programs.WaitForProgram(program);
            }

            renderer.Pipeline.SetProgramSeparate(command._stage, program?.Base);
        }
    }
}
