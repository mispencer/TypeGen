﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TypeGen.Core.Converters;
using TypeGen.Core.Extensions;
using TypeGen.Core.TypeAnnotations;

namespace TypeGen.Core.Services
{
    /// <summary>
    /// Contains logic related to generating and processing TypeScript file contents
    /// </summary>
    internal class FileContentService
    {
        private readonly TypeDependencyService _typeDependencyService;
        private readonly TypeService _typeService;
        private readonly TemplateService _templateService;

        private const string KeepTsTagName = "keep-ts";

        public FileContentService(TypeDependencyService typeDependencyService, TypeService typeService, TemplateService templateService)
        {
            _typeDependencyService = typeDependencyService;
            _typeService = typeService;
            _templateService = templateService;
        }

        /// <summary>
        /// Gets code for the 'imports' section for a given type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="outputDir"></param>
        /// <param name="fileNameConverters"></param>
        /// <param name="typeNameConverters"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">Thrown when one of: type, fileNameConverters or typeNameConverters is null</exception>
        public string GetImportsText(Type type, string outputDir, TypeNameConverterCollection fileNameConverters, TypeNameConverterCollection typeNameConverters)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (fileNameConverters == null) throw new ArgumentNullException(nameof(fileNameConverters));
            if (typeNameConverters == null) throw new ArgumentNullException(nameof(typeNameConverters));

            string result = GetTypeDependencyImportsText(type, outputDir, fileNameConverters, typeNameConverters);
            result += GetCustomImportsText(type);

            if (!string.IsNullOrEmpty(result))
            {
                result += "\r\n";
            }

            return result;
        }

        /// <summary>
        /// Returns TypeScript imports source code related to type dependencies.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="outputDir"></param>
        /// <param name="fileNameConverters"></param>
        /// <param name="typeNameConverters"></param>
        /// <returns></returns>
        private string GetTypeDependencyImportsText(Type type, string outputDir, TypeNameConverterCollection fileNameConverters, TypeNameConverterCollection typeNameConverters)
        {
            var result = "";
            IEnumerable<TypeDependencyInfo> typeDependencies = _typeDependencyService.GetTypeDependencies(type);

            foreach (TypeDependencyInfo typeDependencyInfo in typeDependencies)
            {
                Type typeDependency = typeDependencyInfo.Type;

                string dependencyOutputDir = GetTypeDependencyOutputDir(typeDependency, outputDir);

                string pathDiff = Utilities.GetPathDiff(outputDir, dependencyOutputDir);
                pathDiff = pathDiff.StartsWith("..\\") ? pathDiff : $"./{pathDiff}";

                string typeDependencyName = typeDependency.Name.RemoveTypeArity();
                string fileName = fileNameConverters.Convert(typeDependencyName, typeDependency);

                string dependencyPath = pathDiff + fileName;
                dependencyPath = dependencyPath.Replace('\\', '/');

                string typeName = typeNameConverters.Convert(typeDependencyName, typeDependency);
                result += _templateService.FillImportTemplate(typeName, "", dependencyPath);
            }

            return result;
        }

        /// <summary>
        /// Gets code for imports that are NOT related to type dependencies
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private string GetCustomImportsText(Type type)
        {
            var result = "";
            IEnumerable<MemberInfo> members = _typeService.GetTsExportableMembers(type);

            IEnumerable<TsTypeAttribute> typeAttributes = members
                .Select(memberInfo => memberInfo.GetCustomAttribute<TsTypeAttribute>())
                .Where(tsTypeAttribute => !string.IsNullOrEmpty(tsTypeAttribute?.ImportPath))
                .Distinct(new TsTypeAttributeComparer());

            foreach (TsTypeAttribute attribute in typeAttributes)
            {
                bool withOriginalTypeName = !string.IsNullOrEmpty(attribute.OriginalTypeName);

                string name = withOriginalTypeName ? attribute.OriginalTypeName : attribute.TypeName;
                string asAlias = withOriginalTypeName ? $" as {attribute.TypeName}" : "";
                result += _templateService.FillImportTemplate(name, asAlias, attribute.ImportPath);
            }

            return result;
        }

        /// <summary>
        /// Gets the output directory for a type dependency
        /// </summary>
        /// <param name="typeDependency"></param>
        /// <param name="exportedTypeOutputDir"></param>
        /// <returns></returns>
        private string GetTypeDependencyOutputDir(Type typeDependency, string exportedTypeOutputDir)
        {
            var dependencyClassAttribute = typeDependency.GetCustomAttribute<ExportTsClassAttribute>();
            var dependencyInterfaceAttribute = typeDependency.GetCustomAttribute<ExportTsInterfaceAttribute>();
            var dependencyEnumAttribute = typeDependency.GetCustomAttribute<ExportTsEnumAttribute>();

            if (dependencyClassAttribute == null && dependencyEnumAttribute == null && dependencyInterfaceAttribute == null)
            {
                return exportedTypeOutputDir;
            }

            return dependencyClassAttribute?.OutputDir
                    ?? dependencyInterfaceAttribute?.OutputDir
                    ?? dependencyEnumAttribute?.OutputDir;
        }

        /// <summary>
        /// Gets custom code for a TypeScript file given by filePath.
        /// Returns an empty string if a file does not exist.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="tabSize"></param>
        /// <returns></returns>
        public string GetCustomCode(string filePath, int tabSize)
        {
            if (!File.Exists(filePath)) return "";

            string content = File.ReadAllText(filePath);
            MatchCollection matches = Regex.Matches(content, $@"\/\/<{KeepTsTagName}>((.|\n|\r|\r\n)+?)\/\/<\/{KeepTsTagName}>", RegexOptions.IgnoreCase);

            string tab = Utilities.GetTabText(tabSize);

            string result = matches
                .Cast<Match>()
                .Aggregate("", (current, match) => current + $"\r\n{tab}{match.Groups[1].Value.Trim()}" + "\r\n");

            if (!string.IsNullOrEmpty(result))
            {
                result = result.Remove(0, 2 + tabSize);
            }
            
            return string.IsNullOrEmpty(result)
                ? ""
                : $"\r\n\r\n{tab}//<{KeepTsTagName}>\r\n{tab}{result}{tab}//</{KeepTsTagName}>";
        }
    }
}
