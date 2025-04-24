using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Backend.Filters
{
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var apiDescription = context.ApiDescription;

            // Look for IFormFile parameters
            if (!operation.Parameters.Any() && !HasFormFileParameters(context, out _))
            {
                return;
            }

            // Check if this operation accepts multipart/form-data based on ConsumesAttribute
            var consumes = context.MethodInfo.GetCustomAttributes<ConsumesAttribute>(true)
                .SelectMany(attr => attr.ContentTypes)
                .Distinct();

            if (!consumes.Any(x => x.ToLower().Contains("multipart/form-data")))
            {
                return;
            }

            // Get IFormFile parameters
            var formFileParams = context.MethodInfo.GetParameters()
                .Where(p => p.ParameterType == typeof(IFormFile) ||
                           (p.ParameterType.IsGenericType && p.ParameterType.GetGenericArguments()[0] == typeof(IFormFile)));

            // Replace parameters with the correct schema
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>(),
                            Required = new HashSet<string>()
                        }
                    }
                },
                Required = true
            };

            var schema = operation.RequestBody.Content["multipart/form-data"].Schema;

            // Process each parameter
            foreach (var param in formFileParams)
            {
                if (param.ParameterType == typeof(IFormFile))
                {
                    // Single file parameter
                    schema.Properties.Add(param.Name, new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    });
                    schema.Required.Add(param.Name);
                }
                else
                {
                    // Collection of files parameter
                    schema.Properties.Add(param.Name, new OpenApiSchema
                    {
                        Type = "array",
                        Items = new OpenApiSchema
                        {
                            Type = "string",
                            Format = "binary"
                        }
                    });
                    schema.Required.Add(param.Name);
                }
            }

            // Remove any existing file parameters to prevent duplicates
            var fileParamNames = formFileParams.Select(p => p.Name.ToLowerInvariant()).ToList();
            foreach (var param in operation.Parameters.ToList())
            {
                if (fileParamNames.Contains(param.Name.ToLowerInvariant()))
                {
                    operation.Parameters.Remove(param);
                }
            }
        }

        private bool HasFormFileParameters(OperationFilterContext context, out IEnumerable<ParameterInfo> formFileParams)
        {
            formFileParams = context.MethodInfo.GetParameters()
                .Where(p => p.ParameterType == typeof(IFormFile) ||
                          (p.ParameterType.IsGenericType && p.ParameterType.GetGenericArguments()[0] == typeof(IFormFile)));

            return formFileParams.Any();
        }
    }
}
