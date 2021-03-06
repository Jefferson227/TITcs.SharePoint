﻿
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web;
using Newtonsoft.Json;
using TITcs.SharePoint.Log;

namespace TITcs.SharePoint.Services
{
    public class HandlerFactory : IHttpHandlerFactory
    {
        private static Type[] _handlerTypes = null;
        private static SharePointServiceSection _serviceSection;
        public HandlerFactory()
        {
            try
            {
                if (_handlerTypes == null)
                {
                    _serviceSection = (SharePointServiceSection)ConfigurationManager.GetSection("sharePointServices/service");

                    if (_serviceSection.FilterType == FilterType.AssemblyName)
                    {
                        if (string.IsNullOrEmpty(_serviceSection.AssemblyName))
                            throw new Exception("AssemblyName not defined");

                        try
                        {
                            _handlerTypes = Assembly.Load(_serviceSection.AssemblyName)
                                .ExportedTypes.Where(i => i.BaseType.Name == "ServiceBase")
                                .ToArray();
                        }
                        catch (Exception e1)
                        {
                            Logger.Unexpected("HandlerFactory._handlerTypes", _serviceSection.AssemblyName);
                        }
                    }
                    else
                    {
                        _handlerTypes = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(t => t.GetTypes())
                            .Where(t => t.IsClass && t.IsPublic && t.Namespace == _serviceSection.Namespace).ToArray();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Unexpected("HandlerFactory.constructor", e.Message);

                ResponseJSON(HttpContext.Current.Response, e);
            }
        }

        public Type[] HandlerTypes
        {
            get { return _handlerTypes; }
        }

        public IHttpHandler GetHandler(HttpContext context, string requestType, string url, string pathTranslated)
        {
            try
            {
                if (_serviceSection.EnableCrossDomain)
                {
                    if (context.Request.UrlReferrer == null)
                        throw new Exception("Invalid Cross Domain");

                    var urlReferrer = string.Format("{0}://{1}", context.Request.UrlReferrer.Scheme,
                        context.Request.UrlReferrer.Authority);

                    //TODO
                }

                string className = Path.GetFileNameWithoutExtension(context.Request.PhysicalPath);

                Logger.Information("HandlerFactory.GetHandler", string.Format("Variable className = {0}", className));

                var type = HandlerTypes.SingleOrDefault(i => i.Name.ToLower() == className.ToLower());

                if (type != null)
                {
                    Logger.Information("HandlerFactory.GetHandler", string.Format("Instance of {0}", type.Name));

                    var handler = (IHttpHandler) Activator.CreateInstance(type);
                    return handler;
                }

                var message = string.Format("The service \"{0}{1}\" not defined", className, Path.GetExtension(context.Request.PhysicalPath));
                
                throw new HttpException(500, message);

            }
            catch (Exception e)
            {
                Logger.Unexpected("HandlerFactory.GetHandler", e.Message);

                ResponseJSON(context.Response, e);
            }

            return null;
        }

        private void ResponseJSON(HttpResponse response, Exception e)
        {
            response.Clear();
            response.ContentType = "application/json; charset=utf-8";

            response.StatusCode = 500;
            response.TrySkipIisCustomErrors = true;

            response.Write(JsonConvert.SerializeObject(new
            {
                status = 500,
                exception = new
                {
                    message = e.Message
                }
            }));
        }
        

        public void ReleaseHandler(IHttpHandler handler)
        {

        }
    }
}
