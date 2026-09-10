using System;

namespace Peter;

public class ShellContextMenuException : Exception
{
	public ShellContextMenuException()
	{
	}

	public ShellContextMenuException(string message)
		: base(message)
	{
	}
}
