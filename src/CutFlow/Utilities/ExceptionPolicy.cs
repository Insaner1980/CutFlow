using System.Runtime.InteropServices;

namespace CutFlow.Utilities;

internal static class ExceptionPolicy
{
    public static bool IsFatal(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OutOfMemoryException or
            InsufficientMemoryException or
            StackOverflowException or
            AccessViolationException or
            SEHException or
            AppDomainUnloadedException or
            BadImageFormatException or
            InvalidProgramException)
        {
            return true;
        }

        if (exception is AggregateException aggregateException &&
            aggregateException.InnerExceptions.Any(IsFatal))
        {
            return true;
        }

        return exception.InnerException is not null && IsFatal(exception.InnerException);
    }
}
