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
using System.Net;
using EasyHttp.Http;
using YouTrackSharp.Infrastructure;
using YouTrackSharp.Server;

namespace YouTrackSharp.Admin
{
    public class UserManagement
    {
        readonly IConnection _connection;

        public UserManagement(IConnection connection)
        {
            _connection = connection;
        }

        public bool CreateUser(string login, string fullName, string email)
        {
            bool result = false;
            HttpResponse response = this._connection.Post(string.Format("admin/user?login={0}&fullName={1}&email={2}", login, fullName, email), string.Empty);
            
            if (response.StatusCode == HttpStatusCode.Created)
            {
                result = true;
            }

            return result;
        }

        public bool CreateUser(string login, string fullName, string email, string password)
        {
            bool result = false;
            
            var response = this._connection.Put(string.Format("admin/user/{0}?fullName={1}&email={2}&password={3}", login, fullName, email, password), string.Empty, string.Empty);

            if(response.StatusCode == HttpStatusCode.Created)
            {
                result = true;
            }

            return result;
        }

        public User GetUserByUserName(string username)
        {
            var user = _connection.Get<User>(String.Format("user/bylogin/{0}", username));

            if (user != null)
            {
                user.Username = username;
                return user;
            }
            throw new InvalidRequestException(Language.Server_GetUserByUserName_User_does_not_exist);
        }

        public IEnumerable<Filter> GetFiltersByUsername(string username)
        {
            return _connection.Get<MultipleFilterWrapper, Filter>(String.Format("user/filters/{0}", username));
        }
    }
}