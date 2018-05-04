CurrentActivity Readme

This plugin provides base functionality for Plugins for Xamarin to gain access to the application's main Activity.

# Gettting Started

When plugin is installed, follow the below steps to initialise in your project. There are two ways to initialize this:

## Main/Base Activity Level
1. Simply call the `Init` method on OnCreate

```
CrossCurrentActivity.Current.Init(this, bundle);
```

## Application Level

1. Add a new C# class file in you project called "MainApplication.cs". 
2. Override the OnCreate method and call the `Init` method
```
#if DEBUG
	[Application(Debuggable = true)]
#else
	[Application(Debuggable = false)]
#endif
	public class MainApplication : Application
	{
		public MainApplication(IntPtr handle, JniHandleOwnership transer)
		  : base(handle, transer)
		{
		}

		public override void OnCreate()
		{
			base.OnCreate();
			CrossCurrentActivity.Current.Init(this);
		}
	}
```
If you already have an "Application" class in your project simply add the Init call. 

The benefit of adding it at the Application level is to get the first events for the application.
