using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            // Step 1: Prompt the user for input and output paths
            string rootDirectory = GetInput("Enter the root directory of your project", Directory.GetCurrentDirectory());
            string modelsPathDefault = Path.Combine(rootDirectory, "Data", "Models");
            string reposPathDefault = Path.Combine(rootDirectory, "Data", "Repos");
            string controllersPathDefault = Path.Combine(rootDirectory, "Controllers");

            string modelsPath = GetInput($"Enter the path to the models directory", modelsPathDefault);
            string reposOutputPath = GetInput($"Enter the output path for repositories", reposPathDefault);
            string controllersOutputPath = GetInput($"Enter the output path for controllers", controllersPathDefault);

            bool overwriteExisting = GetConfirmation("Do you want to overwrite existing files? (y/n)", false);

            // Step 2: Prompt for the base class to filter on
            string baseClassName = GetInput("Enter the base class name to filter models", "AuditableEntity");

            // Step 3: Validate paths
            if (!Directory.Exists(modelsPath))
            {
                Console.WriteLine($"Models directory not found: {modelsPath}");
                return;
            }

            // Step 4: Process model files
            var modelFiles = Directory.GetFiles(modelsPath, "*.cs", SearchOption.AllDirectories);

            foreach (var modelFile in modelFiles)
            {
                // Parse the model file
                var code = File.ReadAllText(modelFile);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                // Find all class declarations
                var classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classNode in classNodes)
                {
                    string modelClassName = classNode.Identifier.Text;

                    // Check if the class inherits from the specified base class
                    if (!InheritsFromBaseClass(classNode, baseClassName))
                        continue;

                    // Generate repository class
                    string repoClassName = $"{modelClassName}Repo";
                    string repoFilePath = Path.Combine(reposOutputPath, $"{repoClassName}.cs");
                    GenerateFile(repoFilePath, GenerateRepositoryClass(modelClassName), overwriteExisting, "Repository");

                    // Generate controller class
                    string controllerClassName = $"{modelClassName}Controller";
                    string controllerFilePath = Path.Combine(controllersOutputPath, $"{controllerClassName}.cs");
                    GenerateFile(controllerFilePath, GenerateControllerClass(modelClassName), overwriteExisting, "Controller");
                }
            }

            Console.WriteLine("Code generation completed.");
        }

        static string GetInput(string prompt, string defaultValue)
        {
            Console.WriteLine($"{prompt} (default: {defaultValue}):");
            string input = Console.ReadLine();
            return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
        }

        static bool GetConfirmation(string prompt, bool defaultValue)
        {
            Console.WriteLine($"{prompt} (default: {(defaultValue ? "y" : "n")}):");
            string input = Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrEmpty(input))
                return defaultValue;
            return input == "y" || input == "yes";
        }

        static bool InheritsFromBaseClass(ClassDeclarationSyntax classNode, string baseClassName)
        {
            // Check base types
            var baseList = classNode.BaseList;
            if (baseList == null)
                return false;

            foreach (var baseType in baseList.Types)
            {
                var typeName = baseType.Type.ToString();
                // Handle cases with namespace (e.g., AB.Models.DbModels.AuditableEntity)
                var simpleTypeName = typeName.Split('.').Last();
                if (simpleTypeName == baseClassName)
                {
                    return true;
                }
            }
            return false;
        }

        static void GenerateFile(string filePath, string content, bool overwrite, string fileType)
        {
            if (File.Exists(filePath) && !overwrite)
            {
                Console.WriteLine($"{fileType} file already exists, skipping: {filePath}");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, content);
                Console.WriteLine($"{fileType} class generated: {filePath}");
            }
        }

        static string GenerateRepositoryClass(string modelClassName)
        {
            string template = @"using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AB.Models;
using AB.Models.DbModels;
using AB.Models.TableModels;

namespace AB.Repositories
{
    public class {ModelClassName}Repo : BaseRepo<{ModelClassName}>
    {
        private readonly ABDbContext _dbContext;

        public {ModelClassName}Repo(ABDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }
    }
}";

            return template.Replace("{ModelClassName}", modelClassName);
        }

        static string GenerateControllerClass(string modelClassName)
        {
            string template = @"using Microsoft.AspNetCore.Mvc;
using AB.Repositories;
using AB.Models.DbModels;
using AB.Models.TableModels;

namespace AB.Controllers
{
    [ApiController]
    [Route(""[controller]"")]
    public class {ModelClassName}Controller : GenericController<{ModelClassName}>
    {
        private readonly {ModelClassName}Repo _{modelVarName}Repository;

        public {ModelClassName}Controller({ModelClassName}Repo repository) : base(repository)
        {
            _{modelVarName}Repository = ({ModelClassName}Repo)repository;
        }
    }
}";

            string modelVarName = Char.ToLowerInvariant(modelClassName[0]) + modelClassName.Substring(1);
            return template.Replace("{ModelClassName}", modelClassName)
                           .Replace("{modelVarName}", modelVarName);
        }
    }
}

