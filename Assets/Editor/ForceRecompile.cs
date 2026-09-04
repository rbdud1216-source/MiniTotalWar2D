using UnityEditor; public class ForceRecompile { [MenuItem("Tools/Force Recompile")] public static void Compile() { UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); } }
