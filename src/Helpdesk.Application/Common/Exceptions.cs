namespace Helpdesk.Application.Common;

public class AuthenticationFailedException(string message) : Exception(message);

public class ForbiddenAccessException(string message) : Exception(message);
