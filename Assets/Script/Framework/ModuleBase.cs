namespace PirateCaptain.Framework
{
    /// <summary>由 GameRoot 持有的应用级模块。模块之间的依赖通过构造函数传入。</summary>
    public abstract class ModuleBase
    {
        /// <summary>注册时在 Unity 主线程调用。依赖的模块应当先注册。</summary>
        public virtual void OnInitialize() { }

        /// <summary>按注册逆序调用。初始化中途失败时也会调用，请允许清理部分资源。</summary>
        public virtual void OnShutdown() { }
    }
}
