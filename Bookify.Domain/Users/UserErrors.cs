﻿using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users
{
    public static class UserErrors
    {
        public static Error NotFound = new("User.NotFound", "The user with the specified identifier was not found");
    }
}
