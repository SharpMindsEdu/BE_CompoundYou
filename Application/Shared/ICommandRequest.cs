using MediatR;

namespace Application.Shared;

public interface ICommandRequestBase;

public interface ICommandRequest : IRequest, ICommandRequestBase;

public interface ICommandRequest<out T> : IRequest<T>, ICommandRequestBase;
