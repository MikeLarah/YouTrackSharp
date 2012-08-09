﻿#region License
// Distributed under the BSD License
// =================================
// 
// Copyright (c) 2010-2011, Hadi Hariri
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of Hadi Hariri nor the
//       names of its contributors may be used to endorse or promote products
//       derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// =============================================================
#endregion

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Net;
using System.Security.Authentication;
using EasyHttp.Http;
using EasyHttp.Infrastructure;
using YouTrackSharp.Projects;
using YouTrackSharp.Server;

namespace YouTrackSharp.Infrastructure
{
    public class Connection : IConnection
    {
        private CookieCollection _authenticationCookie;
        private readonly string _host;
        private readonly int _port;
        private readonly IUriConstructor _uriConstructor;
        private string _username;

        public HttpStatusCode HttpStatusCode { get; private set; }

        public Connection(string host, int port = 80, bool useSSL = false, string path = null)
        {
            var protocol = "http";

            _host = host;
            _port = port;


            if (useSSL)
            {
                protocol = "https";
            }

            _uriConstructor = new DefaultUriConstructor(protocol, _host, _port, path);
        }

        public bool IsAuthenticated { get; private set; }

        public void Authenticate(string username, string password)
        {
            IsAuthenticated = false;
            _username = String.Empty;
            _authenticationCookie = null;

            dynamic credentials = new ExpandoObject();

            credentials.login = username;
            credentials.password = password;

            try
            {
                HttpClient response = MakePostRequest("user/login", credentials, HttpContentTypes.ApplicationXml);

                if (response.Response.StatusCode == HttpStatusCode.OK)
                {
                    if (
                        String.Compare(response.Response.DynamicBody.login, "ok",
                                       StringComparison.CurrentCultureIgnoreCase) != 0)
                    {
                        throw new AuthenticationException(Language.YouTrackClient_Login_Authentication_Failed);
                    }
                    IsAuthenticated = true;
                    _authenticationCookie = response.Response.Cookie;
                    _username = username;
                }
                else
                {
                    throw new AuthenticationException(response.Response.StatusDescription);
                }

            }
            catch (HttpException exception)
            {
                throw new AuthenticationException(exception.StatusDescription);
            }
        }

        public dynamic Get(string command)
        {
            var httpRequest = CreateHttpRequest();

            try
            {
                var dynamicBody = httpRequest.Get(_uriConstructor.ConstructBaseUri(command)).DynamicBody();

                HttpStatusCode = httpRequest.Response.StatusCode;

                return dynamicBody;
            }
            catch (HttpException httpException)
            {
                if (httpException.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new InvalidRequestException(Language.Connection_Get_Insufficient_rights);
                }
                throw;
            }
        }

        public T Get<T>(string command)
        {
            var httpRequest = CreateHttpRequest();

            try
            {
                var staticBody = httpRequest.Get(_uriConstructor.ConstructBaseUri(command)).StaticBody<T>();

                HttpStatusCode = httpRequest.Response.StatusCode;

                return staticBody;
            }
            catch (HttpException httpException)
            {
                if (httpException.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new InvalidRequestException(Language.Connection_Get_Insufficient_rights);
                }
                throw;
            }
        }

        public IEnumerable<TInternal> Get<TWrapper, TInternal>(string command)
            where TWrapper : class, IDataWrapper<TInternal>
        {
            var response = Get<TWrapper>(command);

            if (response != null)
            {
                return response.Data;
            }
            return new List<TInternal>();
        }


        public void Head(string command)
        {
            var httpRequest = CreateHttpRequest();

            httpRequest.Head(_uriConstructor.ConstructBaseUri(command));
            HttpStatusCode = httpRequest.Response.StatusCode;
        }

        public void Logout()
        {
            IsAuthenticated = false;
            _username = null;
            _authenticationCookie = null;
        }

        public void PostFile(string command, string path)
        {
            var httpRequest = CreateHttpRequest();

            httpRequest.Request.Accept = HttpContentTypes.ApplicationXml;

            var contentType = GetFileContentType(path);

            var files = new List<FileData>()
                            {
                                new FileData()
                                    {
                                        FieldName = "file",
                                        Filename = path,
                                        ContentTransferEncoding = "binary",
                                        ContentType = contentType
                                    }
                            };

            httpRequest.Post(_uriConstructor.ConstructBaseUri(command), null, files);
            HttpStatusCode = httpRequest.Response.StatusCode;

        }

        public HttpResponse Post(string command, object data)
        {
            // This actually doesn't return Application/XML...Bug in YouTrack
            var client = MakePostRequest(command, data, HttpContentTypes.ApplicationXml);

            return client.Response;
        }

        public dynamic Post(string command, object data, string accept)
        {
            var client = MakePostRequest(command, data, accept);

            return client.Response.DynamicBody;
        }

        public HttpResponse Put(string command, object data, string accept)
        {
            var client = MakePutRequest(command, data, accept);

            return client.Response;
        }

        public User GetCurrentAuthenticatedUser()
        {
            var user = Get<User>("user/current");

            if (user != null)
            {
                user.Username = _username;

                return user;
            }

            return null;
        }

        private string GetFileContentType(string filename)
        {
            var mime = "application/octetstream";
            var extension = Path.GetExtension(filename);

            if (extension != null)
            {
                var ext = extension.ToLower();
                var rk = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
                if (rk != null && rk.GetValue("Content Type") != null)
                    mime = rk.GetValue("Content Type").ToString();
            }

            return mime;
        }

        private HttpClient MakePutRequest(string command, object data, string accept)
        {
            var client = CreateHttpRequest();

            client.Request.Accept = accept;

            client.Put(_uriConstructor.ConstructBaseUri(command), data, HttpContentTypes.ApplicationXWwwFormUrlEncoded);

            return client;
        }

        private HttpClient MakePostRequest(string command, object data, string accept)
        {
            var client = CreateHttpRequest();

            client.Request.Accept = accept;

            client.Post(_uriConstructor.ConstructBaseUri(command), data,
                             HttpContentTypes.ApplicationXWwwFormUrlEncoded);

            return client;
        }


        private HttpClient CreateHttpRequest()
        {
            var httpClient = new HttpClient();

            httpClient.Request.Accept = HttpContentTypes.ApplicationJson;

            httpClient.ThrowExceptionOnHttpError = true;

            if (_authenticationCookie != null)
            {
                httpClient.Request.Cookies = new CookieCollection {_authenticationCookie};
            }

            return httpClient;
        }
    }
}