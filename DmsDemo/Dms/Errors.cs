namespace DmsDemo.Dms;

public class DmsException : Exception
{
    public DmsException(string message) : base(message) { }
}

public class ValidationException : DmsException
{
    public ValidationException(string message) : base(message) { }
}

public class InvalidTransitionException : DmsException
{
    public InvalidTransitionException(string message) : base(message) { }
}

public class NoDriverAvailableException : DmsException
{
    public NoDriverAvailableException(string message) : base(message) { }
}

public class NotFoundException : DmsException
{
    public NotFoundException(string message) : base(message) { }
}