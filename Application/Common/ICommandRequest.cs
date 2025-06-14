using MediatR;

namespace Application.Common;

public interface ICommandRequestBase;

public interface ICommandRequest : IRequest, ICommandRequestBase;

public interface ICommandRequest<out T> : IRequest<T>, ICommandRequestBase;
