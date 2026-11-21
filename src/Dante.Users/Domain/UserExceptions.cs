namespace Dante.Users;

public sealed class InvalidUsernameException(string message) : Exception(message);

public sealed class InvalidPasswordException(string message) : Exception(message);

public sealed class UsernameAlreadyInUseException() : Exception("Username is already in use.");

public sealed class InvalidCredentialsException() : Exception("Invalid username or password.");
