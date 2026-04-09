namespace GalaWallet.Models;

public class NetworkResult<T>
{
	public bool IsSuccess { get; private set; }
	public T Data { get; private set; } = default!;
	public NetworkErrorKind ErrorKind { get; private set; }
	public string ErrorMessage { get; private set; } = "";
	public int HttpStatusCode { get; private set; }

	public static NetworkResult<T> Success(T data) => new()
	{
		IsSuccess = true,
		Data = data
	};

	public static NetworkResult<T> Rejected(string message, int statusCode = 0) => new()
	{
		IsSuccess = false,
		ErrorKind = NetworkErrorKind.Rejected,
		ErrorMessage = message,
		HttpStatusCode = statusCode
	};

	public static NetworkResult<T> TransportError(string message) => new()
	{
		IsSuccess = false,
		ErrorKind = NetworkErrorKind.TransportError,
		ErrorMessage = message
	};

	public static NetworkResult<T> ParseError(string message) => new()
	{
		IsSuccess = false,
		ErrorKind = NetworkErrorKind.ParseError,
		ErrorMessage = message
	};
}

public enum NetworkErrorKind
{
	Rejected,
	TransportError,
	ParseError
}
