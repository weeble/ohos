using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;

namespace OpenHome.Os
{
    public interface IAction
    {
        void Invoke();
    }

    public interface IAction<T>
    {
        void Invoke(T aArg1);
    }
    
    public interface IFunc<T> { T Invoke(); }
    public interface IFunc<T1, T2> { T2 Invoke(T1 aArg1); }
    public interface IFunc<T1, T2, T3> { T3 Invoke(T1 aArg1, T2 aArg2); }
    public interface IFunc<T1, T2, T3, T4> { T4 Invoke(T1 aArg1, T2 aArg2, T3 aArg3); }
    public interface IFunc<T1, T2, T3, T4, T5> { T5 Invoke(T1 aArg1, T2 aArg2, T3 aArg3, T4 aArg4); }


    public class ActionMock
    {
        public Action Action { get; private set; }
        public Mock<IAction> Mock { get; private set; }
        public ActionMock()
        {
            Mock = new Mock<IAction>();
            Action = Mock.Object.Invoke;
        }
    }

    public class ActionMock<T>
    {
        public Action<T> Action { get; private set; }
        public Mock<IAction<T>> Mock { get; private set; }
        public ActionMock()
        {
            Mock = new Mock<IAction<T>>();
            Action = Mock.Object.Invoke;
        }
    }

    public class FuncMock<T>
    {
        public Func<T> Func { get; private set; }
        public Mock<IFunc<T>> Mock { get; private set; }
        public FuncMock()
        {
            Mock = new Mock<IFunc<T>>();
            Func = Mock.Object.Invoke;
        }
    }

    public class FuncMock<T1, T2>
    {
        public Func<T1, T2> Func { get; private set; }
        public Mock<IFunc<T1, T2>> Mock { get; private set; }
        public FuncMock()
        {
            Mock = new Mock<IFunc<T1, T2>>();
            Func = Mock.Object.Invoke;
        }
    }

    public class FuncMock<T1, T2, T3>
    {
        public Func<T1, T2, T3> Func { get; private set; }
        public Mock<IFunc<T1, T2, T3>> Mock { get; private set; }
        public FuncMock()
        {
            Mock = new Mock<IFunc<T1, T2, T3>>();
            Func = Mock.Object.Invoke;
        }
    }

    public class FuncMock<T1, T2, T3, T4>
    {
        public Func<T1, T2, T3, T4> Func { get; private set; }
        public Mock<IFunc<T1, T2, T3, T4>> Mock { get; private set; }
        public FuncMock()
        {
            Mock = new Mock<IFunc<T1, T2, T3, T4>>();
            Func = Mock.Object.Invoke;
        }
    }

    public class FuncMock<T1, T2, T3, T4, T5>
    {
        public Func<T1, T2, T3, T4, T5> Func { get; private set; }
        public Mock<IFunc<T1, T2, T3, T4, T5>> Mock { get; private set; }
        public FuncMock()
        {
            Mock = new Mock<IFunc<T1, T2, T3, T4, T5>>();
            Func = Mock.Object.Invoke;
        }
    }
}
