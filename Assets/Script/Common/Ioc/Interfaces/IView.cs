namespace ForeverNine.IoC
{
    /// <summary>
    /// View 标记接口 - 所有 View 接口和实现都应该继承或实现此接口
    /// 用于标识这是一个 UI 视图组件，区别于业务逻辑和服务
    /// </summary>
    /// <remarks>
    /// 使用方式：
    /// 1. View 接口应继承此接口：public interface IMainView : IView { }
    /// 2. View 实现类应继承 BaseView（BaseView 已实现 IView）
    /// 3. 在 Container 中使用 BindView 方法绑定 View 类型
    /// 
    /// 示例：
    /// public interface IMainView : IView { }
    /// public class MainView : BaseView, IMainView { }
    /// Container.BindView&lt;IMainView, MainView&gt;();
    /// </remarks>
    public interface IView
    {
    }
}

